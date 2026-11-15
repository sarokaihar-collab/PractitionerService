# Архитектура и принятые решения

Документ описывает архитектуру сервиса, FHIR-специфику, кэширование, асинхронный импорт,
качество кода, безопасность, языковые конвенции и осознанные ограничения.

Краткий гайд по запуску и проверке — в [`../README.md`](../README.md).

## Архитектура

Clean Architecture + DDD. Четыре проекта с правилом зависимостей «внутрь»:

```
Api ──► Application ──► Domain
 │            ▲
 └──► Infrastructure ──┘
```

- **Domain** — сущность `Practitioner` (плоская модель строго по ТЗ 3.1), доменные
  инварианты, `DomainException`. Не знает ни про EF Core, ни про FHIR, ни про ASP.NET.
- **Application** — use cases (MediatR-команды и запросы), read-модель и command-result,
  абстракции `IPractitionerRepository`, `IUnitOfWork`, `ICacheService`, `IDateTimeProvider`.
- **Infrastructure** — EF Core, репозиторий, UnitOfWork, миграции, сидинг, Redis-кэш,
  Redis Streams consumer, `IDateTimeProvider`. Зависит от Application и Domain.
- **Api** — контроллеры, FHIR POCO, маппер FHIR ↔ команды, middleware для `OperationOutcome`,
  Swagger-описания. Зависит от Application и Infrastructure.

DI по слоям: `AddApplication()` / `AddInfrastructure(configuration)` + стандартный ASP.NET Core DI.

### CQRS: разделение моделей

- **Write-сторона** — команды возвращают `PractitionerCommandResult(Id, Version)` — минимум,
  необходимый для `Location` и `ETag`.
- **Read-сторона** — запросы возвращают `PractitionerReadModel` — единая read-модель для
  `GetById` и `Search`.

Команды не зависят от формы чтения. Read-модель можно заменить на денормализованную
(materialized view) без правок write-стороны.

### Домен — FHIR-agnostic

Домен содержит только плоские поля из ТЗ (firstName, lastName, phone, speciality, placeOfWork,
email, isActive) и служебные (createdAt, updatedAt, version, isDeleted, sourceEventId).
`HumanName`, `ContactPoint`, `CodeableConcept` — FHIR-типы, живут **только** в Api-слое.
Маппинг Domain ↔ FHIR — единственное место, где эти миры встречаются.

### Design-time DbContext factory

`PractitionerDbContextFactory : IDesignTimeDbContextFactory<PractitionerDbContext>`
используется **только** инструментом `dotnet ef` для генерации миграций.
В рантайме `DbContext` создаётся через DI с реальной строкой подключения.

- Строка подключения читается из env var `ConnectionStrings__Default` (тот же, что
  у приложения), fallback — локальный placeholder для `dotnet ef migrations add`
  (к БД не подключается).
- **Никаких кредов в коде.** Placeholder — не секрет, реальные креды — из env vars
  или `dotnet user-secrets`.

## Порты

Стандартные порты PostgreSQL (5432) и Redis (6379) часто заняты на хост-машине
другими сервисами. Хост-порты задаются через `.env` (см. `.env.example`):

| Сервис | Стандартный | Хост-порт | Переменная |
|---|---|---|---|
| PostgreSQL | 5432 | **5434** | `POSTGRES_HOST_PORT` |
| Redis | 6379 | **6380** | `REDIS_HOST_PORT` |
| API | 8080 | **8081** | `API_HOST_PORT` |

- **`.env` не коммитится** (в `.gitignore`). Шаблон — `.env.example`.
- **Между контейнерами** используются стандартные порты `5432` / `6379` (внутренняя сеть Docker).
- **С хост-машины** (F5 в IDE, psql, redis-cli, Postman) — порты из `.env`.
- При смене портов в `.env` обновите и `appsettings.Development.json`.

## FHIR-специфика

Сервис реализует FHIR R5 Practitioner REST API
(см. https://hl7.org/fhir/R5/practitioner.html и http://hl7.org/fhir/R5/http.html).

### Эндпоинты

| Метод | URL | Описание | Успех |
|---|---|---|---|
| `POST` | `/Practitioner` | Создать | `201 Created` + `Location` + `ETag` |
| `GET` | `/Practitioner/{id}` | Прочитать | `200 OK` + `ETag` |
| `PUT` | `/Practitioner/{id}` | Обновить (full) | `200 OK` + `ETag` |
| `DELETE` | `/Practitioner/{id}` | Soft delete | `204 No Content` |
| `GET` | `/Practitioner?...` | Поиск | `200 OK` + `Bundle(searchset)` |

### Медиа-типы

- Принимаются: `application/fhir+json`, `application/json`.
- Отдаётся: `application/fhir+json`.

### Маппинг модели домена на FHIR R5

| Поле ТЗ | Поле FHIR | Комментарий |
|---|---|---|
| `id` | `Practitioner.id` | |
| `firstName` | `Practitioner.name[0].given[0]` | |
| `lastName` | `Practitioner.name[0].family` | |
| `phone` | `Practitioner.telecom[system=phone].value` | |
| `email` | `Practitioner.telecom[system=email].value` | |
| `isActive` | `Practitioner.active` | Если не указан при создании — `true` |
| `speciality` | `Practitioner.qualification[0].code.text` | |
| `placeOfWork` | `extension[url=.../place-of-work].valueString` | В R5 у Practitioner нет прямого «места работы» |
| `createdAt` | `extension[url=.../created-at].valueString` | ISO 8601 |
| `updatedAt` | `meta.lastUpdated` | |
| `version` | `meta.versionId` | |
| `sourceEventId` | `extension[url=.../source-event-id].valueString` | Служебное, для идемпотентности |

**Массивы FHIR:** берём только первый элемент (`name[0]`, `given[0]`, первый
`telecom[system=phone]`, первый `telecom[system=email]`, `qualification[0]`).
Остальные **молча игнорируются**. В проде — либо явное отклонение (`422`), либо
хранение всех значений (изменение доменной модели).

### Семантика HTTP-статусов

- **404 Not Found** — ресурс с таким `id` **никогда не существовал**.
- **410 Gone** — ресурс существовал, но был **удалён** (soft delete, `isDeleted = true`).
  FHIR R5-семантика. Это же — при повторном `DELETE`.
- **409 Conflict** — конфликт версий (optimistic concurrency через `If-Match`), либо дубль
  `sourceEventId` / `email` / `phone`.
- **422 Unprocessable Entity** — нарушение доменных инвариантов или структуры FHIR
  (обязательные поля отсутствуют, некорректный email, невалидный `If-Match`, несовпадение
  `id` в теле и URL).

### OperationOutcome вместо ProblemDetails

Все ошибки возвращаются в FHIR-формате `OperationOutcome`:

```json
{
  "resourceType": "OperationOutcome",
  "issue": [
    {
      "severity": "error",
      "code": "not-found",
      "diagnostics": "Practitioner ... не найден."
    }
  ]
}
```

**Почему не `ProblemDetails` (RFC 7807):**

- `OperationOutcome` — часть контракта FHIR, ожидаемая клиентами FHIR. `ProblemDetails` —
  конвенция ASP.NET Core, не входящая в спецификацию FHIR.
- FHIR-клиенты (HAPI, Firely, любые медицинские интеграции) разбирают `OperationOutcome` стандартно.
- `OperationOutcome.issue.code` использует стандартизированные значения (`not-found`, `deleted`,
  `conflict`, `duplicate`, `required`, `structure`, `business-rule`, `exception`).

Маппинг «тип исключения → HTTP status + FHIR issue code» — единый `ExceptionMap` в Api-слое.

### Пагинация

Stateless: `_count` (по умолчанию 20, максимум 100) и `_offset` (по умолчанию 0). В ответе
`Bundle` содержит `link[self]`, `link[previous]` (если `offset > 0`), `link[next]` (если есть
ещё записи). Ссылки самодостаточны — все исходные параметры поиска в них сохранены.

### Идемпотентность

- `POST` — заголовок `Idempotency-Key`. При повторе с тем же ключом возвращается тот же ресурс.
  Реализовано через `sourceEventId` в `extension` (приоритет) или `idem:{key}` из заголовка.
  Проверка в `CreatePractitionerCommandHandler` + unique-индекс в БД. Гонка на индексе ловится
  и превращается в возврат существующего ресурса.
- `PUT` — идемпотентен по природе.
- `DELETE` — идемпотентен: повторный `DELETE` на уже удалённый ресурс даёт `410 Gone`, но не
  меняет данные.

`Idempotency-Key` ограничен 200 символами. При превышении — `422` с `code=structure`.
Внутри БД `source_event_id` хранится в `VARCHAR(255)`.

### Optimistic concurrency

- `ETag` на `GET`/`POST`/`PUT` ответах — `"{version}"`.
- `PUT` с `If-Match: "N"` → если версия не совпадает, `409 Conflict`. Двойная защита:
  сравнение в handler + `IsConcurrencyToken` на `Version` в EF Core.
- `If-Match: *` — «обновить, если ресурс существует» (existence проверяется через `404`/`410`,
  версия не сверяется).
- Некорректный `If-Match` (например, `"abc"` или `""`) → `422` `code=structure`.

### Уникальность и консистентность

- **Email** уникален среди активных (не удалённых) врачей. Дубль → `409 Conflict`
  с `diagnostics` «Врач с таким email уже существует.»
- **Phone** уникален среди активных. Дубль → `409 Conflict`
  с `diagnostics` «Врач с таким телефоном уже существует.»
- **ФИО и специальность НЕ уникальны** — в реальном мире возможны гомонимы.
- Уникальные индексы — **partial** (`WHERE is_deleted = false`): soft-deleted записи не блокируют
  повторную регистрацию с тем же email/phone.

**Нормализация телефона:** в домене (`Practitioner.Create`/`Update`) телефон приводится
к `+<digits>`: удаляются все нецифровые символы, ведущий `+` сохраняется.
`+7 (900) 100-00-01` → `+79001000001`. Это позволяет unique-индексу ловить разные
форматы одного телефона.

**Валидация email** — через `System.Net.Mail.MailAddress.TryCreate` (RFC 5322-парсер из .NET).

### Восстановление удалённых

**Запрещено через CRUD.** `PUT`/`POST` для soft-deleted ресурса возвращает `410 Gone`.
Для восстановления потребуется отдельный админский эндпоинт (не реализован — вне скоупа
тестового).

### `PUT` без `If-Match` — last-write-wins

Если клиент не передал `If-Match`, optimistic concurrency **не проверяется** — последний
запрос перезаписывает предыдущий. FHIR не требует `If-Match` для `PUT`. Для конкурентных
клиентов рекомендуется всегда передавать `If-Match: "<version>"`.

### `PUT` — сверка `id`

Если в теле `PUT` передан `id`, он должен совпадать с `id` из URL. Иначе — `422` (`code=structure`).
Это часть FHIR-контракта.

## Кэширование

`GET /Practitioner/{id}` кэшируется в Redis с TTL 5 минут.

- Абстракция — `ICacheService` (Application), реализация — `RedisCacheService` (Infrastructure)
  поверх `IDistributedCache`.
- Инвалидация — в `UpdatePractitionerCommandHandler` и `DeletePractitionerCommandHandler`:
  после `SaveChanges` удаляется ключ `practitioner:id:{guid}`.
- `404` и `410` **не кэшируются** — дешёвые, а кэшировать «не найдено» опасно (гонки с созданием).
- `GET /Practitioner` (search) **сознательно не кэшируется** — результат зависит от десятка
  параметров, инвалидация при любом изменении любого врача делает кэш бесполезным.

### Гонка cache-aside (multi-instance)

При обновлении кэш инвалидируется **после** `SaveChanges`. Между `SaveChanges` и `RemoveAsync`
есть окно, в котором параллельный `GET` может прочитать **старую** строку из БД и положить её
в кэш. На single-instance окно микроскопическое; при нескольких инстансах — реальный риск на
несколько миллисекунд.

В проде: (а) двойная инвалидация (`RemoveAsync` до и после `SaveChanges`);
(б) versioned keys (`practitioner:id:{guid}:v{version}`); (в) distributed lock на обновление
ключа. Для тестового — cache-aside без этих ухищрений, риск осознан.

## Асинхронный импорт через Redis Streams

Помимо REST, сервис принимает события импорта через Redis Stream `practitioner-events`.
Реализует требование ТЗ «поддержка асинхронного импорта данных через шину сообщений».

### Формат сообщения

```json
{
  "eventId": "evt-import-001",
  "type": "created",
  "payload": {
    "firstName": "Иван",
    "lastName": "Иванов",
    "phone": "+7-900-100-00-01",
    "speciality": "Кардиолог",
    "placeOfWork": "ГКБ №1",
    "email": "i.ivanov@clinic1.ru",
    "isActive": true
  }
}
```

- `eventId` → `sourceEventId` в домене. Ключ идемпотентности.
- `type` — сейчас поддерживается только `created`.

### Идемпотентность

Повторная доставка сообщения с тем же `eventId` **не создаёт дубль**.
`CreatePractitionerCommandHandler` при существующем `sourceEventId` возвращает уже созданного
врача, consumer делает `XACK`.

### Ограничения consumer'а

- `XACK` делается **даже при ошибке обработки** (чтобы «битое» сообщение не висело в Pending).
  В проде нужен retry counter + dead-letter queue + `XAUTOCLAIM` для recovery.
- Только `type=created`. Расширение на `updated`/`deleted` — механический `switch` в
  `ProcessAsync` + маппинг на `UpdatePractitionerCommand`/`DeletePractitionerCommand`.
- Polling-модель (`Task.Delay(500)` + `XREADGROUP`), не `XREADGROUP BLOCK`. Для тестового —
  проще; в проде — `BLOCK` экономит запросы к Redis.
- JSON-ошибки логируются с содержимым payload (первые 500 символов) для диагностики.

## Индексы

В таблице `practitioners` созданы:

- **PK** по `id`.
- **Unique partial** `ux_practitioners_email_active` (`WHERE is_deleted = false`).
- **Unique partial** `ux_practitioners_phone_active` (`WHERE is_deleted = false`).
- **Unique partial** `ux_practitioners_source_event_id` (`WHERE source_event_id IS NOT NULL`).
- **GIN trigram** для `first_name`, `last_name`, `speciality`, `place_of_work`
  (`USING gin (... gin_trgm_ops)`), partial `WHERE is_deleted = false`.

**Почему GIN trigram, а не B-tree:** поиск по подстроке (`ILIKE '%pattern%'`) не может
использовать обычный B-tree (не работает с ведущим `%`). GIN trigram индекс (`pg_trgm`
+ `gin_trgm_ops`) специально создан для `LIKE`/`ILIKE` с ведущим `%` и используется
всеми поисковыми запросами через `EF.Functions.ILike`.

**B-tree индексы по `first_name`/`last_name`/`speciality`/`place_of_work` не созданы** —
они не используются поисковыми запросами.

## Качество кода

### Анализаторы и конфигурация

- **`Directory.Build.props`** — единая точка подключения пакетов анализаторов:
  Microsoft.CodeAnalysis.NetAnalyzers, Microsoft.EntityFrameworkCore.Analyzers,
  StyleCop, Roslynator, Meziantou.Analyzer, SonarAnalyzer.
- **`.editorconfig`** в корне решения — стиль, именование, severity per-rule.
  Современный формат, заменяет устаревший `.ruleset` (deprecated с .NET 5).

### Уровни severity

- **`error`** — build-breaking: EF Core internals (`EF1001`/`EF1002`), SQL injection
  (`CA2100`), `async void` (`VSTHRD100`).
- **`warning`** — resource management, async, globalization, correctness (~20 правил).
- **`suggestion`** — стиль и low-risk запахи.
- **`none`** — StyleCop documentation rules (XML-документация пишется только там,
  где видна в Swagger — Api-слой).

### CI

GitHub Actions (`.github/workflows/ci.yml`) прогоняет:
- `dotnet format --verify-no-changes --severity warning` — форматирование;
- `dotnet build -c Release` — сборка;
- `dotnet test tests/PractitionerService.UnitTests` — Unit-тесты с покрытием (Codecov);
- `docker build` — сборка образа (на `push`).

## Безопасность

### Креды в репозитории

- **`appsettings.json`** (production) — без кредов. Все секреты приходят из env vars.
  Если обязательная настройка отсутствует — приложение падает при старте
  (`ValidateOnStart` на `DatabaseOptions` / `RedisOptions`).
- **`appsettings.Development.json`** — локальные dev-креды (`postgres`/`postgres`)
  для docker-compose Postgres. Безопасно коммитить, потому что (а) это dev-only аккаунт,
  (б) контейнер слушает только `localhost`, (в) нет реальных данных.
  В крупных проектах этот файл был бы в `.gitignore` и заменён на
  `appsettings.Development.example.json`.
- **`docker-compose.yml`** — креды из `.env` (`POSTGRES_USER`, `POSTGRES_PASSWORD`)
  с dev-fallback'ами. `.env` в `.gitignore`; в git только `.env.example`.
- **`PractitionerDbContextFactory`** (design-time) — использует нерабочий placeholder-пароль.
  Реальный `dotnet ef database update` требует явной установки `ConnectionStrings__Default`.

## Конфигурация

Все инфраструктурные настройки описаны через **Options pattern** с `ValidateOnStart`:

- `DatabaseOptions` (`ConnectionStrings:Default`) — обязательна, `[Required]`.
- `RedisOptions` (`Redis:Configuration`, `Redis:InstanceName`) — `Configuration` обязательна.

При отсутствии или невалидности конфигурации приложение **падает при старте**
(`OptionsValidationException`), а не возвращает 500 на первом запросе.

Значения приходят из:
- `appsettings.json` (без кредов) — базовая конфигурация;
- `appsettings.Development.json` (dev-креды) — перекрывает при `ASPNETCORE_ENVIRONMENT=Development`;
- env vars (`ConnectionStrings__Default`, `Redis__Configuration`) — приоритет выше,
  используются в Docker Compose.

## Версионирование

Проект следует [Semantic Versioning 2.0.0](https://semver.org/).
История изменений — в [`../CHANGELOG.md`](../CHANGELOG.md).
Лицензия — MIT (см. [`../LICENSE`](../LICENSE)).

## Тесты

| Проект | Что покрывает |
|---|---|
| `UnitTests` (Domain) | Инварианты `Practitioner`: валидация, нормализация email/phone, soft delete, optimistic concurrency в сущности |
| `UnitTests` (Application) | Handlers: idempotent replay, cache hit/miss, конфликт версий, clamp пагинации, инвалидация кэша |
| `UnitTests` (Api) | `PractitionerFhirMapper`: маппинг туда-обратно, FHIR-валидация, генерация `Bundle.link` |
| `UnitTests` (Architecture) | NetArchTest: правило зависимостей Clean Architecture |

## Ограничения и принятые решения

### Архитектурные

- **`PractitionerEventConsumer` — fat class (SRP).** Совмещает управление Redis Stream и
  dispatch в MediatR. Сознательно: один тип события. При добавлении `updated`/`deleted` —
  рефакторинг в pipeline `IEventHandler`.
- **`UnitOfWork` совмещает save + exception mapping.** `SaveChangesAsync` транслирует
  `DbUpdateException` в доменные типы. Это единый шов работы с БД; разделение на
  `IExceptionMapper` спекулятивно.

### Функциональные

- **`PUT` без `If-Match` — last-write-wins** (см. FHIR-специфика).
- **Массивы FHIR** — берём только первый элемент (см. FHIR-специфика).

### Инфраструктурные

- **`ILike` + GIN trigram** (см. Индексы). Требует расширения `pg_trgm` — Postgres-specific.
  Если понадобится MSSQL/MySQL — заменить на full-text search провайдера или
  нормализованные поля.
- **API не стартует без БД** — `depends_on: condition: service_healthy` + healthcheck `pg_isready`.
- **Healthcheck** проверяет доступность БД (`AddDbContextCheck`). Это readiness, не liveness.
- **Миграции при старте** — `Database.MigrateAsync()` внутри `IDbInitializer`. Для продакшена —
  отдельный init-контейнер / job.
- **Сидинг идемпотентен** — если в таблице есть хоть одна запись, сидинг пропускается.
- **Креды в `appsettings.Development.json`** — тестовые, не секрет. В проде — secrets manager.

### Версии пакетов

- **MediatR 12**, не 13 — в 13-й лицензионные изменения для крупных организаций.
- **.NET 10** — LTS.

## Структура

```
PractitionerService/
├── src/
│   ├── PractitionerService.Domain/
│   ├── PractitionerService.Application/
│   ├── PractitionerService.Infrastructure/
│   └── PractitionerService.Api/
├── tests/
│   ├── PractitionerService.UnitTests/          # Domain, Application, Api, Architecture
├── docs/
│   ├── ARCHITECTURE.md                       # этот файл
│   └── Practitioner.postman_collection.json
├── .github/
│   └── workflows/ci.yml
├── CHANGELOG.md
├── LICENSE
├── Dockerfile
├── docker-compose.yml
├── .env.example
├── .dockerignore
├── .gitignore
└── README.md
```