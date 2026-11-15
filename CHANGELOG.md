# Changelog

All notable changes to this project will be documented in this file.
The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [1.0.0] - 2026-11-13

### Added

- FHIR R5 Practitioner REST API: create, read, update, delete, search.
- `Bundle(searchset)` response with stateless pagination (`_count`, `_offset`).
- `OperationOutcome` error format (instead of `ProblemDetails`).
- Idempotency for `POST` via `Idempotency-Key` / `sourceEventId` (≤ 200 chars).
- Optimistic concurrency via `ETag` / `If-Match`, `If-Match: *` support.
- Soft delete with FHIR `410 Gone` semantics.
- Unique `email` and `phone` (partial indexes, `WHERE is_deleted = false`).
- Phone normalization to E.164-like format in the domain.
- Redis caching for `GET /Practitioner/{id}` (TTL 5 min, invalidated on write).
- Redis Streams async import (`practitioner-events`) with idempotent consumer.
- GIN trigram indexes (`pg_trgm`) for case-insensitive substring search.
- Seed data loaded from `SeedData/practitioners.json` on first run.
- Swagger UI with Russian descriptions.
- Postman collection (11 requests: health, CRUD, validation, idempotency).

### Security

- Non-root user in the Docker runtime image (`$APP_UID`).
- No credentials in source — design-time factory reads from env vars.
- SQL injection and `async void` rules promoted to `error` in `.editorconfig`.

### Infrastructure

- .NET 10, EF Core 10 (Npgsql), MediatR 12.
- Multi-stage Dockerfile; `docker-compose` with healthchecks and `depends_on`.
- Roslyn analyzers (Microsoft, EF Core, StyleCop, Roslynator, Meziantou, SonarAnalyzer)
  configured via `.editorconfig` + `Directory.Build.props`.
- Architecture tests (NetArchTest) enforce the Clean Architecture dependency rule.
- Unit and integration tests (xUnit, NSubstitute, FluentAssertions, Testcontainers).
- GitHub Actions CI (build, format check, tests, coverage, Docker build).