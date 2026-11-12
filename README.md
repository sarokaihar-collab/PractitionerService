# Practitioner Service

Backend-сервис для хранения и управления справочником врачей в высоконагруженной системе.
Реализует FHIR R5-совместимый REST API (`Practitioner` resource), кэширование GET-запросов
через Redis и асинхронный импорт данных через Redis Streams.

## Стек

- **.NET 10** / ASP.NET Core Web API
- **PostgreSQL 16** через EF Core 10 (Npgsql)
- **Redis 7** — кэш + брокер сообщений (Redis Streams)
- **MediatR 12** — CQRS
- **Docker / Docker Compose**
- **Swagger** (описания на русском)
- **xUnit + NSubstitute + FluentAssertions + Testcontainers** — тесты

## Архитектура

Clean Architecture + DDD. Четыре проекта с правилом зависимостей «внутрь»:

```
Api ──► Application ──► Domain
 │            ▲
 └──► Infrastructure ──┘
```

- **Domain** — сущность `Practitioner` (плоская модель строго по ТЗ 3.1), доменные инварианты,
  `DomainException`. Не знает ни про EF Core, ни про FHIR, ни про ASP.NET.
- **Application** — use cases (MediatR-команды и запросы), read-модель и команды-result,
  абстракции `IPractitionerRepository`, `IUnitOfWork`, `ICacheService`, `IDateTimeProvider`.
- **Infrastructure** — EF Core, репозиторий, UnitOfWork, миграции, сидинг, Redis-кэш,
  Redis Streams consumer, `IDateTimeProvider`. Зависит от Application и Domain.
- **Api** — контроллеры, FHIR POCO, маппер FHIR ↔ команды, middleware для `OperationOutcome`,
  Swagger-описания. Зависит от Application и Infrastructure.

DI по слоям: `AddApplication()` / `AddInfrastructure(configuration)` / стандартный ASP.NET Core DI.

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
`HumanName`, `ContactPoint`, `CodeableConcept` — это FHIR-типы, они живут **только** в Api-слое.
Маппинг Domain ↔ FHIR — единственное место, где эти миры встречаются.

## Запуск

### Через docker-compose (рекомендуемый)

```bash
cp .env.example .env      # если хотите переопределить порты
docker compose up --build
```

- API: http://localhost:8081
- Swagger UI: http://localhost:8081/swagger
- Healthcheck: http://localhost:8081/health
- PostgreSQL: `localhost:5434`
- Redis: `localhost:6380`

При первом запуске:

1. Compose поднимает PostgreSQL и Redis, ждёт `healthy` по healthcheck.
2. Запускается API, применяет EF Core миграции (`Database.MigrateAsync`).
3. Сидинг загружает 10 тестовых врачей из `SeedData/practitioners.json`.
4. Стартует `PractitionerEventConsumer` (BackgroundService) и подписывается на Redis Stream.

### Локально (F5 в IDE)

1. Поднять инфраструктуру в Docker:
   ```bash
   docker compose up -d postgres redis
   ```
2. Проверить `src/PractitionerService.Api/appsettings.Development.json` — строки подключения
   смотрят на `localhost:5434` (Postgres) и `localhost:6380` (Redis).
3. Открыть `PractitionerService.sln`, выбрать startup project `PractitionerService.Api`, F5.

`ASPNETCORE_ENVIRONMENT=Development` → Swagger включён, `appsettings.Development.json`
переопределяет порты.

## Порты

Стандартные порты PostgreSQL (5432) и Redis (6379) часто заняты на хост-машине
другими сервисами. Хост-порты задаются через `.env` (см. `.env.example`):

| Сервис | Стандартный | Хост-порт | Переменная |
|---|---|---|---|
| PostgreSQL | 5432 | **5434** | `POSTGRES_HOST_PORT` |
| Redis | 6379 | **6380** | `REDIS_HOST_PORT` |
| API | 8080 | **8081** | `API_HOST_PORT` |

- **`.env` не коммитится** (в `.gitignore`). Шаблон — `.env.example`.
- **Между контейнерами** используются стандартные `5432` / `6379` (внутренняя сеть Docker,
  не меняется).
- **С хост-машины** (F5 в IDE, psql, redis-cli, Postman) — порты из `.env`.
- При смене портов в `.env` обновите и `appsettings.Development.json`.

## FHIR-специфика

Сервис реализует FHIR R5 Practitioner REST API (см. https://hl7.org/fhir/R5/practitioner.html
и http://hl7.org/fhir/R5/http.html).

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
| `placeOfWork` | `extension[url=.../place-of-work].valueString` | В FHIR R5 у Practitioner нет прямого «места работы»; используем extension |
| `createdAt` | `meta.lastUpdated` | |
| `updatedAt` | `meta.lastUpdated` | |
| `version` | `meta.versionId` | |
| `sourceEventId` | `extension[url=.../source-event-id].valueString` | Служебное, для идемпотентности импорта |

**Важно:** домен не содержит `HumanName`/`ContactPoint`/`CodeableConcept`. Это чистые FHIR-типы,
они живут только в API-слое. Домен — плоская модель строго по ТЗ 3.1.

### Семантика HTTP-статусов

- **404 Not Found** — ресурс с таким `id` **никогда не существовал**.
- **410 Gone** — ресурс существовал, но был **удалён** (soft delete, `isDeleted = true`).
  FHIR R5-семантика. Это же — при повторном `DELETE`.
- **409 Conflict** — конфликт версий (optimistic concurrency через `If-Match`), либо дубль
  `sourceEventId` / `email` / `phone`.
- **422 Unprocessable Entity** — нарушение доменных инвариантов или структуры FHIR
  (обязательные поля отсутствуют, некорректный email).

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

- `OperationOutcome` — часть контракта FHIR, ожидаемая клиентами FHIR. `ProblemDetails` — конвенция
  ASP.NET Core, не входящая в спецификацию FHIR.
- FHIR-клиенты (HAPI, Firely, любые медицинские интеграции) разбирают `OperationOutcome` стандартно.
- `OperationOutcome.issue.code` использует стандартизированные значения (`not-found`, `deleted`,
  `conflict`, `duplicate`, `required`, `structure`, `business-rule`, `exception`) — машинно-читаемая
  семантика, которой нет в `ProblemDetails.detail`.

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
- `DELETE` — идемпотентен: повторный `DELETE` на уже удалённый ресурс даёт `410 Gone`, но не меняет
  данные.

### Optimistic concurrency

- `ETag` на `GET`/`POST`/`PUT` ответах — `"{version}"`.
- `PUT` с `If-Match: "N"` → если версия не совпадает, `409 Conflict`. Двойная защита: сравнение
  в handler + `IsConcurrencyToken` на `Version` в EF Core.

### Уникальность и консистентность

- **Email** уникален среди активных (не удалённых) врачей. Дубль → `409 Conflict`.
- **Phone** уникален среди активных. Дубль → `409 Conflict`.
- **ФИО и специальность НЕ уникальны** — в реальном мире возможны гомонимы.
- Уникальные индексы — **partial** (`WHERE is_deleted = false`): soft-deleted записи не блокируют
  повторную регистрацию с тем же email/phone.

### Восстановление удалённых

**Запрещено через CRUD.** `PUT`/`POST` для soft-deleted ресурса возвращает `410 Gone`. Для
восстановления потребуется отдельный админский эндпоинт (не реализован — вне скоупа тестового).

## Кэширование

`GET /Practitioner/{id}` кэшируется в Redis с TTL 5 минут.

- Абстракция — `ICacheService` (Application), реализация — `RedisCacheService` (Infrastructure)
  поверх `IDistributedCache`.
- Инвалидация — в `UpdatePractitionerCommandHandler` и `DeletePractitionerCommandHandler`:
  после `SaveChanges` удаляется ключ `practitioner:id:{guid}`.
- `404` и `410` **не кэшируются** — дешёвые, а кэшировать «не найдено» опасно (гонки с созданием).
- `GET /Practitioner` (search) **сознательно не кэшируется** — результат зависит от десятка
  параметров, инвалидация при любом изменении любого врача делает кэш бесполезным. Возможная
  оптимизация — короткий TTL 30–60 с без инвалидации (eventual consistency), но не критично.

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
- `type` — сейчас поддерживается только `created`. `updated` и `deleted` — очевидное расширение
  (маппинг на `UpdatePractitionerCommand` / `DeletePractitionerCommand`), не реализовано.

### Отправка тестового сообщения

```bash
docker exec -it practitionerservice-redis redis-cli XADD practitioner-events '*' data '{
  "eventId": "evt-import-001",
  "type": "created",
  "payload": {
    "firstName": "Асинхронный",
    "lastName": "Врач",
    "phone": "+7-999-111-22-33",
    "speciality": "Терапевт",
    "placeOfWork": "Клиника",
    "email": "async@test.ru",
    "isActive": true
  }
}'
```

Consumer стартует автоматически (`AddHostedService`). Отдельная команда для запуска не нужна.

### Идемпотентность

Повторная доставка сообщения с тем же `eventId` **не создаёт дубль**.
`CreatePractitionerCommandHandler` при существующем `sourceEventId` возвращает уже созданного
врача, consumer делает `XACK`.

### Почему Redis Streams, а не Kafka

- Redis уже нужен для кэша — не поднимаем второй контейнер.
- Redis Streams — полноценная очередь с consumer groups, `XACK`, `XAUTOCLAIM`.
- Для тестового задания хватает с запасом.

Переход на Kafka — замена одного адаптера в Infrastructure (`PractitionerEventConsumer`).
Application и Domain не меняются.

## Языковые конвенции

- **Логи, сообщения исключений, тексты ошибок API (`OperationOutcome.diagnostics`)** — русский.
- **README, Swagger-описания, XML-документация на Api-слой** — русский (клиентский контракт).
- **Идентификаторы в коде, inline-комментарии `//`, XML-документация на Domain и Application** —
  английский.
- **Системные логи ASP.NET Core / EF Core** — английский (платформенные, не переводим).

## Тесты

```bash
dotnet test tests/PractitionerService.UnitTests
```

| Проект | Что покрывает |
|---|---|
| `UnitTests` (Domain) | Инварианты `Practitioner`: валидация, нормализация email, soft delete, optimistic concurrency в сущности |
| `UnitTests` (Application) | Handlers: idempotent replay, cache hit/miss, конфликт версий, clamp пагинации, инвалидация кэша |
| `UnitTests` (Api) | `PractitionerFhirMapper`: маппинг туда-обратно, FHIR-валидация, генерация `Bundle.link` |

**Сознательно не покрыто:**

- `RedisCacheService` — тонкая обёртка над `IDistributedCache`, косвенно проверена через
  `GetById`-тесты (cache hit/miss).
- `PractitionerEventConsumer` — требует реального Redis Stream + фонового потока. Ручная проверка
  через `XADD` описана выше.
- HTTP-слой через `WebApplicationFactory` — избыточно: Postman-коллекция покрывает сценарии.

## Postman

`docs/Practitioner.postman_collection.json` — 11 запросов в 4 папках:

- **Health** — `GET /health` → `200 Healthy`
- **CRUD** — Create → Get → Search → Update → Delete → Get deleted (`410 Gone`)
- **Validation** — дубль email и дубль телефона → `409 Conflict` (`OperationOutcome code=duplicate`)
- **Idempotency** — Create с фиксированным `Idempotency-Key` → Replay с тем же ключом → тот же `id`

Импорт: Postman → **File → Import**. Base URL по умолчанию — `http://localhost:8081`
(переменная `baseUrl` на вкладке **Variables**).

Запуск: правой кнопкой по коллекции → **Run collection** → **Run**.
Все 11 запросов должны быть зелёные.

## Тестовые данные

`src/PractitionerService.Infrastructure/SeedData/practitioners.json` — 10 врачей с
уникальными email и телефонами. Загружается при первом запуске (если таблица `practitioners`
пуста). Сидинг идемпотентен — при рестарте контейнера данные не перезаписываются.

## Ограничения и принятые решения

- **`ILike` не используется.** Поиск регистронезависимый через `ToLower().Contains()` — работает
  во всех СУБД (PostgreSQL, MSSQL, MySQL, SQLite). На больших объёмах не использует B-tree-индексы;
  для продакшена — функциональные индексы (`CREATE INDEX ... ON practitioners (LOWER(last_name))`)
  или `pg_trgm` + полнотекстовый поиск.
- **Non-root в Docker** — `USER $APP_UID` (встроенный пользователь `app` в `aspnet:10.0`).
- **API не стартует без БД** — `depends_on: condition: service_healthy` + healthcheck `pg_isready`.
- **Healthcheck** проверяет доступность БД (`AddDbContextCheck`). Это readiness, не liveness.
- **Миграции при старте** — `Database.MigrateAsync()` внутри `IDbInitializer`. Для продакшена —
  отдельный init-контейнер / job.
- **Сидинг идемпотентен** — если в таблице есть хоть одна запись, сидинг пропускается.
- **Валидация — в домене, без FluentValidation.** `Practitioner.Create`/`Update` бросают
  `DomainException` с русским сообщением.
- **MediatR 12**, не 13 (в 13-й лицензионные изменения для крупных организаций).
- **Парсинг `If-Match` примитивный** (`W/"N"` / `"N"` → int).
- **Нет аутентификации / rate limiting / OpenTelemetry** — по условиям тестового не требуется.
- **Redis Streams:** `XACK` делается даже при ошибке обработки (чтобы «битое» сообщение не висело
  в Pending). В проде нужен retry counter + dead-letter queue + `XAUTOCLAIM`.
- **Только `type=created`** в consumer. Update/delete — механическое расширение, не реализовано.

## Структура проекта

```
PractitionerService/
├── src/
│   ├── PractitionerService.Domain/           # сущность Practitioner, DomainException
│   ├── PractitionerService.Application/      # MediatR handlers, DTO, абстракции
│   ├── PractitionerService.Infrastructure/   # EF Core, Redis cache, Redis Stream consumer
│   └── PractitionerService.Api/              # контроллер, FHIR POCO, middleware
├── tests/
│   ├── PractitionerService.UnitTests/        # Domain, Application, Api (mappers)
├── docs/
│   └── Practitioner.postman_collection.json
├── .env.example
├── .dockerignore
├── .gitignore
├── Directory.Build.props
├── Dockerfile
├── docker-compose.yml
└── README.md
```