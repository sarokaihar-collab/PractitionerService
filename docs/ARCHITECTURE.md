# Архитектура и принятые решения

Документ описывает архитектуру сервиса, FHIR-специфику, кэширование, асинхронный импорт,
языковые конвенции и осознанные ограничения.

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
- Некорректный `If-Match` (например, `"abc"`) → `422` `code=structure`.

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

### Cache-aside race (multi-instance)

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

### Поток

```
Внешняя система ──XADD──► Redis Stream "practitioner-events"
                                  │
                                  ▼
                       PractitionerEventConsumer (BackgroundService)
                                  │
                                  ▼
                       IMediator.Send(CreatePractitionerCommand)
                                  │
                                  ▼
                            PostgreSQL
```

Consumer — тонкая прослойка: только десериализация + вызов существующего MediatR-handler.
Никакой новой бизнес-логики.

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

### Почему Redis Streams, а не Kafka

- Redis уже нужен для кэша — не поднимаем второй инфраструктурный контейнер.
- Redis Streams — полноценная очередь с consumer groups, `XACK`, `XAUTOCLAIM`.
- Для тестового задания хватает с запасом.

Переход на Kafka — замена одного адаптера в Infrastructure (`PractitionerEventConsumer`).
Application и Domain не меняются.

### Ограничения consumer'а

- `XACK` делается **даже при ошибке обработки** (чтобы «битое» сообщение не висело в Pending).
  В проде нужен retry counter + dead-letter queue + `XAUTOCLAIM` для recovery.
- Только `type=created`. Расширение на `updated`/`deleted` — механический `switch` в
  `ProcessAsync` + маппинг на `UpdatePractitionerCommand`/`DeletePractitionerCommand`.
- Polling-модель (`Task.Delay(500)` + `XREADGROUP`), не `XREADGROUP BLOCK`. Для тестового —
  проще; в проде — `BLOCK` экономит запросы к Redis.

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

**Компромисс:** GIN-индексы больше по размеру и замедляют INSERT/UPDATE, но дают
кардинальное ускорение поиска на больших объёмах. При необходимости перейти на
другой механизм — `tsvector` + GIN (полнотекстовый поиск) или внешняя поисковая
система (Elasticsearch / Meilisearch).

**B-tree индексы по `first_name`/`last_name`/`speciality`/`place_of_work` не созданы** —
они не используются поисковыми запросами.

## Языковые конвенции

- **Логи, сообщения исключений, тексты ошибок API (`OperationOutcome.diagnostics`)** — русский.
- **README, ARCHITECTURE.md, Swagger-описания, XML-документация на Api-слой** — русский.
- **Идентификаторы в коде, inline-комментарии `//`, XML-документация на Domain и Application** — английский.
- **Системные логи ASP.NET Core / EF Core** — английский (платформенные).

## Тесты

| Проект | Что покрывает |
|---|---|
| `UnitTests` (Domain) | Инварианты `Practitioner`: валидация, нормализация email/phone, soft delete, optimistic concurrency в сущности |
| `UnitTests` (Application) | Handlers: idempotent replay, cache hit/miss, конфликт версий, clamp пагинации, инвалидация кэша |
| `UnitTests` (Api) | `PractitionerFhirMapper`: маппинг туда-обратно, FHIR-валидация, генерация `Bundle.link` |
| `IntegrationTests` | PostgreSQL через Testcontainers: unique email, unique phone, optimistic concurrency на уровне БД |

**Сознательно не покрыто:**

- `RedisCacheService` — тонкая обёртка над `IDistributedCache`, косвенно проверена через
  `GetById`-тесты (cache hit/miss).
- `PractitionerEventConsumer` — требует реального Redis Stream + фонового потока. Ручная
  проверка через `XADD` (см. README).
- HTTP-слой через `WebApplicationFactory` — избыточно: Postman-коллекция покрывает сценарии.

## Ограничения и принятые решения

### Архитектурные

- **`PractitionerEventConsumer` — fat class (SRP).** Совмещает управление Redis Stream и
  dispatch в MediatR. Сознательно: один тип события. При добавлении `updated`/`deleted` —
  рефакторинг в pipeline `IEventHandler`.
- **`UnitOfWork` совмещает save + exception mapping.** `SaveChangesAsync` транслирует
  `DbUpdateException` в доменные типы. Это единый шов работы с БД; разделение на
  `IExceptionMapper` спекулятивно.
- **`PractitionerFhirMapper` — статический.** Pure functions без зависимостей. Если появятся
  зависимости (например, чтение конфигурации FHIR-профиля) — переведём в `IPractitionerFhirMapper`.
- **`ExceptionMap` — единый source of truth.** Маппинг «тип исключения → HTTP + FHIR code»
  в одном словаре. Middleware и `FhirOperationOutcomeFactory` читают из него.

### Функциональные

- **`name` / `family` / `given` — семантика фильтров.** `name` ищет по `first_name` OR
  `last_name`. `family` и `given` — уточняющие (AND с предыдущими):
  `?name=Иван&family=Петров` вернёт тех, у кого фамилия содержит «Петров» И
  (имя или фамилия содержит «Иван»).
- **Pagination — offset-based.** `_offset=N&_count=M` → SQL `OFFSET N LIMIT M`. На больших `N`
  (> ~10 000) замедляется. В проде — cursor-based (`_cursor=<last_seen_id>`) или FHIR
  `_page`-токены.
- **`PUT` без `If-Match` — last-write-wins** (см. FHIR-специфика).
- **Массивы FHIR** — берём только первый элемент (см. FHIR-специфика).

### Инфраструктурные

- **`ILike` + GIN trigram** (см. Индексы). Требует расширения `pg_trgm` — Postgres-specific.
  Если понадобится MSSQL/MySQL — заменить на full-text search провайдера или
  нормализованные поля.
- **Non-root в Docker** — `USER $APP_UID` (встроенный пользователь `app` в `aspnet:10.0`).
- **API не стартует без БД** — `depends_on: condition: service_healthy` + healthcheck `pg_isready`.
- **Healthcheck** проверяет доступность БД (`AddDbContextCheck`). Это readiness, не liveness.
- **Миграции при старте** — `Database.MigrateAsync()` внутри `IDbInitializer`. Для продакшена —
  отдельный init-контейнер / job.
- **Сидинг идемпотентен** — если в таблице есть хоть одна запись, сидинг пропускается.
- **Креды в `appsettings.Development.json`** — тестовые, не секрет. В проде — secrets manager.

### Сознательно не реализовано

- **Аутентификация / авторизация** — по условиям тестового не требуется.
- **Rate limiting** — не требуется.
- **OpenTelemetry / метрики** — не требуется.
- **DLQ + retry counter** в consumer'е — задокументировано, в проде обязательно.
- **Полнотекстовый поиск** (опциональная часть ТЗ) — вместо него GIN trigram для подстроки.

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
│   ├── PractitionerService.UnitTests/
│   └── PractitionerService.IntegrationTests/
├── docs/
│   ├── ARCHITECTURE.md                       # этот файл
│   └── Practitioner.postman_collection.json
├── Dockerfile
├── docker-compose.yml
├── .env.example
├── .dockerignore
├── .gitignore
├── Directory.Build.props
└── README.md
```