# Practitioner Service

[![CI](https://github.com/sarokaihar-collab/PractitionerService/actions/workflows/ci.yml/badge.svg)](https://github.com/sarokaihar-collab/PractitionerService/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](./LICENSE)
[![.NET 10](https://img.shields.io/badge/.NET-10.0-purple.svg)](https://dotnet.microsoft.com/download/dotnet/10.0)

Backend-сервис для хранения справочника врачей. Реализует FHIR R5-совместимый REST API
(`Practitioner` resource), кэширование GET-запросов через Redis и асинхронный импорт
через Redis Streams.

CI прогоняет: `dotnet format --verify-no-changes`, `dotnet build -c Release`, Unit-тесты
с покрытием (Codecov), сборку Docker-образа. 

## Стек

- **.NET 10** / ASP.NET Core Web API
- **PostgreSQL 16** + расширение **`pg_trgm`** (GIN trigram индексы для поиска по подстроке)
- **EF Core 10** (Npgsql)
- **Redis 7** — кэш + брокер сообщений (Redis Streams)
- **MediatR 12** — CQRS
- **Docker / Docker Compose**
- **Swagger** (описания на русском)
- **Roslyn Analyzers** — Microsoft.CodeAnalysis.NetAnalyzers, EF Core analyzers,
  StyleCop, Roslynator, Meziantou.Analyzer, SonarAnalyzer
- **xUnit + NSubstitute + FluentAssertions + Testcontainers + NetArchTest** — тесты
- **GitHub Actions** — CI (build, format check, unit tests, coverage, Docker build)

## Требования

- **Docker Desktop** (или Docker Engine + Docker Compose v2)
- Свободные порты на хост-машине: **5434** (PostgreSQL), **6380** (Redis), **8081** (API)

Для локальной разработки (F5 в IDE) дополнительно: **.NET 10 SDK**.

Стандартные порты `5432` / `6379` / `8080` не используются — они часто заняты
другими сервисами (локальный PostgreSQL, Keycloak и т.п.). Порты настраиваются через
`.env` (см. `.env.example`).

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

**PowerShell** (рекомендуется для Windows):

```powershell
$payload = '{"eventId":"evt-import-001","type":"created","payload":{"firstName":"Асинхронный","lastName":"Врач","phone":"+7-999-111-22-33","speciality":"Терапевт","placeOfWork":"Клиника","email":"async@test.local","isActive":true}}'
docker exec -i practitionerservice-redis redis-cli XADD practitioner-events * data $payload
```

**Bash** (Git Bash, WSL, Linux/macOS):

```bash
docker exec -i practitionerservice-redis redis-cli XADD practitioner-events '*' data '{
  "eventId": "evt-import-001",
  "type": "created",
  "payload": {
    "firstName": "Асинхронный",
    "lastName": "Врач",
    "phone": "+7-999-111-22-33",
    "speciality": "Терапевт",
    "placeOfWork": "Клиника",
    "email": "async@test.local",
    "isActive": true
  }
}'
```

> ⚠️ **PowerShell:** не используйте `\"` внутри одинарных кавычек — JSON попадёт
> в Redis сломанным, consumer упадёт с `JsonReaderException`. Используйте переменную
> `$payload` (см. выше).

В логах API через 1–2 секунды: `Event ... (evt-import-001) успешно обработан.`

Проверить: `curl "http://localhost:8081/Practitioner?name=Асинхронный"` → `total: 1`.

## Локальная разработка (F5 в IDE)

1. Поднять только инфраструктуру:
   ```bash
   docker compose up -d postgres redis
   ```
2. Проверить `src/PractitionerService.Api/appsettings.Development.json`:
   `Host=localhost;Port=5434`, `Redis:Configuration: "localhost:6380"`.
3. Открыть `PractitionerService.sln`, startup project — `PractitionerService.Api`, F5.

Swagger включён (`ASPNETCORE_ENVIRONMENT=Development`).

## Безопасность

### Креды в development vs production

Креды в `docker-compose.yml`, `.env.example` и `appsettings.Development.json`
(`postgres` / `postgres`) — **локальные development-дефолты** для docker-compose
контейнера Postgres. Они не предназначены для реального развёртывания.

- **`appsettings.json`** (production config) **не содержит кредов**.
  Строки подключения приходят из env var `ConnectionStrings__Default`.
- **`appsettings.Development.json`** содержит локальные dev-креды и перекрывается
  env vars в Docker Compose.
- **`.env`** в `.gitignore`; в git только `.env.example`.
- **`PractitionerDbContextFactory`** использует placeholder-пароль
  (`__SET_VIA_ENV__`); реальные design-time операции требуют явной установки
  `ConnectionStrings__Default`.

## Языковые конвенции

- **Логи, сообщения исключений, тексты ошибок API (`OperationOutcome.diagnostics`)** — русский.
- **README, ARCHITECTURE.md, Swagger-описания, XML-документация на Api-слой** — русский.
- **Идентификаторы в коде, inline-комментарии `//`, XML-документация на Domain и Application** — английский.
- **Конфиги** (`.editorconfig`, `Directory.Build.props`, `docker-compose.yml`,
  `appsettings*.json`) — комментарии на английском (инфраструктурные файлы,
  не пользовательская документация).
- **Системные логи ASP.NET Core / EF Core** — английский (платформенные).

## Структура

```
PractitionerService/
├── src/
│   ├── PractitionerService.Domain/            # сущность Practitioner, DomainException
│   ├── PractitionerService.Application/       # MediatR handlers, DTO, абстракции
│   ├── PractitionerService.Infrastructure/    # EF Core, Redis, consumer
│   └── PractitionerService.Api/               # контроллер, FHIR POCO, middleware
├── tests/
│   ├── PractitionerService.UnitTests/         # Domain, Application, Api, Architecture
├── docs/
│   ├── ARCHITECTURE.md
│   └── Practitioner.postman_collection.json
├── .github/
│   └── workflows/ci.yml                       # GitHub Actions CI
├── CHANGELOG.md
├── LICENSE                                    # MIT
├── Dockerfile
├── docker-compose.yml
├── .env.example
├── .dockerignore
├── .gitignore
└── README.md
```

## Документация

- [`docs/ARCHITECTURE.md`](./docs/ARCHITECTURE.md) — архитектура, FHIR-специфика,
  принятые решения, ограничения, code quality.
- [`CHANGELOG.md`](./CHANGELOG.md) — история версий.
- [`LICENSE`](./LICENSE) — MIT.
- [`docs/Practitioner.postman_collection.json`](./docs/Practitioner.postman_collection.json)
  — Postman-коллекция.