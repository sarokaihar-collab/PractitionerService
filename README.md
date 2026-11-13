# Practitioner Service

Backend-сервис для хранения справочника врачей. Реализует FHIR R5-совместимый REST API
(`Practitioner` resource), кэширование GET-запросов через Redis и асинхронный импорт
через Redis Streams.

## Стек

- **.NET 10** / ASP.NET Core Web API
- **PostgreSQL 16** + расширение `pg_trgm` (GIN trigram индексы для поиска)
- **EF Core 10** (Npgsql)
- **Redis 7** — кэш + брокер сообщений (Redis Streams)
- **MediatR 12** — CQRS
- **Docker / Docker Compose**
- **Swagger** (описания на русском)
- **xUnit + NSubstitute + FluentAssertions + Testcontainers + NetArchTest** — тесты
- **Roslyn Analyzers** (Microsoft, EF Core, StyleCop, Roslynator, Meziantou, SonarAnalyzer)

## Требования

- Docker Desktop (или Docker Engine + Docker Compose v2)
- Свободные порты на хост-машине: **5434** (PostgreSQL), **6380** (Redis), **8081** (API)

Стандартные `5432` / `6379` / `8080` часто заняты другими сервисами. Порты настраиваются
через `.env` (см. `.env.example`).

## Запуск

```bash
cp .env.example .env      # опционально — если нужно поменять порты
docker compose up --build
```

Первый запуск — ~3–5 минут (сборка образа + инициализация БД).

**Автоматически:**

1. Compose поднимает PostgreSQL и Redis, ждёт `healthy`.
2. API применяет EF Core миграции (`Database.MigrateAsync`).
3. Сидинг загружает 10 тестовых врачей из `SeedData/practitioners.json`.
4. Стартует `PractitionerEventConsumer` (BackgroundService) и подписывается на Redis Stream.

**Куда смотреть:**

| Что | URL |
|---|---|
| API | http://localhost:8081 |
| Swagger UI | http://localhost:8081/swagger |
| Healthcheck | http://localhost:8081/health |
| PostgreSQL | `localhost:5434`, `postgres` / `postgres`, db `practitioners` |
| Redis | `localhost:6380` |

## Проверка

### 1. Health

```bash
curl http://localhost:8081/health
# Ожидаемо: Healthy
```

### 2. Postman

Импорт: **File → Import** → `docs/Practitioner.postman_collection.json`.

Проверить `baseUrl = http://localhost:8081` (вкладка **Variables** коллекции).

Запуск: правый клик по коллекции → **Run collection** → **Run**.

**11 запросов, все должны быть зелёные:**

- Health
- CRUD: Create → Get → Search → Update → Delete → Get deleted (`410 Gone`)
- Validation: дубль email и дубль телефона (`409 Conflict`)
- Idempotency: Create с фиксированным ключом → Replay (тот же `id`)

Коллекция самоочищающаяся — повторные прогоны работают без сброса БД.

### 3. Асинхронный импорт (Redis Streams)

```bash
$payload = '{"eventId":"evt-import-001","type":"created","payload":{"firstName":"Асинхронный","lastName":"Врач","phone":"+7-999-111-22-33","speciality":"Терапевт","placeOfWork":"Клиника","email":"async@test.local","isActive":true}}'
docker exec -i practitionerservice-redis redis-cli XADD practitioner-events * data $payload
```

В логах API через 1–2 секунды: `Event ... (evt-import-001) успешно обработан.`

Проверка: `curl "http://localhost:8081/Practitioner?name=Асинхронный"` → `total: 1`.

> **PowerShell:** не используйте `\"` внутри одинарных кавычек — JSON попадёт в Redis
> сломанным. Отправляйте через переменную `$payload` (см. выше).

## Тесты

```bash
dotnet test tests/PractitionerService.UnitTests
# Domain + Application + Api mappers + Architecture tests — без Docker (~25 тестов)

dotnet test tests/PractitionerService.IntegrationTests
# PostgreSQL через Testcontainers + concurrency + unique constraints (~5 тестов) — требует Docker
```

## Локальная разработка (F5 в IDE)

1. Поднять только инфраструктуру:
   ```bash
   docker compose up -d postgres redis
   ```
2. Проверить `src/PractitionerService.Api/appsettings.Development.json`:
   `Host=localhost;Port=5434`, `Redis:Configuration: "localhost:6380"`.
3. Открыть `PractitionerService.sln`, startup project — `PractitionerService.Api`, F5.

Swagger включён (`ASPNETCORE_ENVIRONMENT=Development`).

## Code quality

- **Анализаторы:** Microsoft.CodeAnalysis.NetAnalyzers, EF Core analyzers, StyleCop,
  Roslynator, Meziantou.Analyzer, SonarAnalyzer — подключены централизованно через
  `Directory.Build.props`.
- **Конфигурация:** единый `.editorconfig` в корне — стиль, именование, severity
  (`error` / `warning` / `suggestion` / `none`).
- **Строгие правила:** EF Core internals (`EF1001`/`EF1002`), SQL injection (`CA2100`),
  `async void` (`VSTHRD100`) — `error`.
- **Автофикс:** `dotnet format --severity info`.
- **Архитектурные тесты** (NetArchTest): Domain не зависит от Application/Infrastructure/Api,
  Application — от Infrastructure/Api, Infrastructure — от Api.

Подробности — в [`docs/ARCHITECTURE.md`](./docs/ARCHITECTURE.md) → раздел «Code quality».

## Структура

```
PractitionerService/
├── src/
│   ├── PractitionerService.Domain/           # сущность Practitioner, DomainException
│   ├── PractitionerService.Application/      # MediatR handlers, DTO, абстракции
│   ├── PractitionerService.Infrastructure/   # EF Core, Redis, consumer
│   └── PractitionerService.Api/              # контроллер, FHIR POCO, middleware
├── tests/
│   ├── PractitionerService.UnitTests/        # Domain, Application, Api, Architecture
├── docs/
│   ├── ARCHITECTURE.md                       # архитектура, решения, ограничения
│   └── Practitioner.postman_collection.json
├── Directory.Build.props
├── .editorconfig
├── Dockerfile
├── docker-compose.yml
├── .env.example
├── .dockerignore
├── .gitignore
└── README.md
```

## Документация

Архитектура, FHIR-специфика, кэширование, асинхронный импорт, code quality,
принятые решения и ограничения — в [`docs/ARCHITECTURE.md`](./docs/ARCHITECTURE.md).