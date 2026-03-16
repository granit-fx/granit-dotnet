---
title: "ADR-007: Testcontainers — Containerized Integration Tests"
description: "Adoption of Testcontainers for ephemeral PostgreSQL containers in integration tests"
sidebar:
  order: 7
  label: "007 - Testcontainers"
---

> **Date:** 2026-02-24
> **Authors:** Jean-Francois Meyers
> **Scope:** granit-dotnet (Granit.Wolverine.Postgresql.IntegrationTests)

## Context

Granit integration tests require a real PostgreSQL database to validate
DBMS-specific behaviors: EF Core migrations, Wolverine outbox, multi-tenant
global filters, JSONB queries, etc.

In-memory alternatives (EF Core InMemory, SQLite) do not faithfully reproduce
PostgreSQL behavior and mask bugs that only appear in production.

## Decision

**Testcontainers** (`Testcontainers.PostgreSql`) to orchestrate ephemeral
PostgreSQL containers in integration tests.

## Alternatives considered

### Option 1: Testcontainers (selected)

- **License**: MIT
- **Advantage**: real PostgreSQL container started on demand, complete
  isolation per test, automatic cleanup, .NET fluent API, xUnit support
  via `IAsyncLifetime`
- **CI**: compatible with GitHub Actions (service containers) and GitLab CI

### Option 2: EF Core InMemory

- **Advantage**: fast, zero infrastructure dependency
- **Disadvantage**: no real SQL (no migrations, no FK constraints,
  no JSONB, no transactions), false sense of confidence,
  bugs masked in production

### Option 3: SQLite (EF Core)

- **Advantage**: real SQL without a server, fast
- **Disadvantage**: SQL dialect different from PostgreSQL (no JSONB,
  no schemas, different types), non-portable migrations,
  different transactional behavior

### Option 4: Shared PostgreSQL test database

- **Advantage**: no Docker, speed (no container startup)
- **Disadvantage**: shared state between tests (difficult isolation),
  manual cleanup, non-reproducible CI (depends on external server),
  conflicts between developers

## Justification

| Criterion | Testcontainers | InMemory | SQLite | Shared DB |
| --------- | -------------- | -------- | ------ | --------- |
| PostgreSQL fidelity | Full | None | Partial | Full |
| Isolation | Per test | Per test | Per test | Difficult |
| CI reproducibility | Yes | Yes | Yes | No |
| Speed | Medium (~3-5s init) | Very fast | Fast | Fast |
| Zero external infra | Yes (Docker) | Yes | Yes | No |
| EF Core migrations | Yes | No | Partial | Yes |

## Consequences

### Positive

- Tests faithful to production behavior (real PostgreSQL)
- Complete isolation: each test suite has its own database
- Reproducible CI without external dependency
- Early detection of DBMS-related bugs (types, constraints, transactions)

### Negative

- Requires Docker on development machines and in CI
- Container startup time (~3-5 seconds per test suite)
- Higher memory consumption than in-memory alternatives
