# Practitioner Service

Backend-сервис для хранения справочника врачей. Реализует FHIR R5-совместимый REST API
(`Practitioner` resource), кэширование GET-запросов через Redis и асинхронный импорт
через Redis Streams.

## Стек

- **.NET 10** / ASP.NET Core Web API
- **PostgreSQL 16** через EF Core 10 (Npgsql) — основное хранилище
- **Redis 7** — кэш + брокер сообщений (Redis Streams)
- **MediatR 12** — CQRS
- **Docker / Docker Compose**
- **Swagger** (описания на русском)
- **xUnit + NSubstitute + FluentAssertions + Testcontainers** — тесты

## Требования

- Docker Desktop (или Docker Engine + Docker Compose v2)
- Свободные порты на хост-машине: **5434** (PostgreSQL), **6380** (Redis), **8081** (API)

Стандартные порты `5432` / `6379` / `8080` не используются — они часто заняты
другими сервисами (локальный PostgreSQL, Keycloak и т.п.). Порты настраиваются через
`.env` (см. `.env.example`).

## Запуск

```bash
cp .env.example .env      # опционально — если нужно поменять порты
docker compose up --build
```

Первый запуск займёт ~3–5 минут (сборка образа + инициализация БД).

**Что произойдёт автоматически:**

1. Compose поднимает PostgreSQL и Redis, ждёт `healthy` по healthcheck.
2. API применяет EF Core миграции (`Database.MigrateAsync`).
3. Сидинг загружает 10 тестовых врачей из `SeedData/practitioners.json`.
4. Стартует `PractitionerEventConsumer` (BackgroundService) и подписывается на Redis Stream.

**Куда смотреть после старта:**

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

Импорт: Postman → **File → Import** → `docs/Practitioner.postman_collection.json`.

Убедиться, что переменная `baseUrl` = `http://localhost:8081` (вкладка **Variables** коллекции).

Запуск: правой кнопкой по коллекции → **Run collection** → **Run**.

**11 запросов, все должны быть зелёные:**

- Health
- CRUD: Create → Get → Search → Update → Delete → Get deleted (`410 Gone`)
- Validation: дубль email и дубль телефона (`409 Conflict`)
- Idempotency: Create с фиксированным ключом → Replay (тот же `id`)

### 3. Асинхронный импорт (Redis Streams)

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
    "email": "async@test.ru",
    "isActive": true
  }
}'
```

В логах API через 1–2 секунды: `Event ... (evt-import-001) успешно обработан.`
Проверить: `curl "http://localhost:8081/Practitioner?name=Асинхронный"` → `total: 1`.

## Тесты

```bash
dotnet test tests/PractitionerService.UnitTests
# Domain + Application + Api-мапперы — не требует Docker

dotnet test tests/PractitionerService.IntegrationTests
# PostgreSQL через Testcontainers — требует запущенный Docker
```

## Локальная разработка (F5 в IDE)

1. Поднять только инфраструктуру:
   ```bash
   docker compose up -d postgres redis
   ```
2. Проверить `src/PractitionerService.Api/appsettings.Development.json` —
   `Host=localhost;Port=5434` (Postgres), `Redis:Configuration: "localhost:6380"`.
3. Открыть `PractitionerService.sln`, startup project — `PractitionerService.Api`, F5.

Swagger включён (`ASPNETCORE_ENVIRONMENT=Development`).

## Структура

```
PractitionerService/
├── src/
│   ├── PractitionerService.Domain/           # сущность Practitioner
│   ├── PractitionerService.Application/      # MediatR handlers, DTO, абстракции
│   ├── PractitionerService.Infrastructure/   # EF Core, Redis, consumer
│   └── PractitionerService.Api/              # контроллер, FHIR POCO, middleware
├── tests/
│   ├── PractitionerService.UnitTests/
│   └── PractitionerService.IntegrationTests/
├── docs/
│   ├── ARCHITECTURE.md                       # архитектура, решения, ограничения
│   └── Practitioner.postman_collection.json
├── Dockerfile
├── docker-compose.yml
├── .env.example
└── README.md
```

## Документация

Архитектура, FHIR-специфика, принятые решения и ограничения —
в [`docs/ARCHITECTURE.md`](./docs/ARCHITECTURE.md).