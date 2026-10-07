# ADR-0007: Database per service on PostgreSQL with EF Core

- **Status:** Accepted
- **Date:** 2026-10-05

## Context

A shared database is the most common source of hidden coupling between microservices: one
team's schema change breaks another service, and nobody owns the data model. Each service
needs transactional consistency for its own aggregates plus its outbox and inbox (ADR-0006).

## Decision

- **Database per service:** Catalog and Ordering each own a separate PostgreSQL database.
  No service reads another's tables, and cross-service data flows through APIs and events.
  Locally they share one server (two databases). In Azure they can share or split Flexible
  Servers independently of the code.
- **PostgreSQL 16** with **EF Core 8 / Npgsql**:
  - snake_case naming (`EFCore.NamingConventions`), the idiomatic convention in PostgreSQL;
  - **optimistic concurrency** via the `xmin` system column as row version: concurrent stock
    reservations get `DbUpdateConcurrencyException` and are retried (409 over HTTP);
  - defense in depth: `CHECK (available_stock >= 0)`;
  - `jsonb` for outbox payloads and a partial index for pending outbox rows.
- **Schema changes as EF Core migrations**, committed to the repository.
  - Development / demo: applied at startup when `Database:MigrateOnStartup=true` (with retries).
  - Production (recommended): build an EF **migration bundle** in CI and run it as a Kubernetes
    `Job` before the rollout, because concurrent replicas migrating at startup can race.

## Consequences

**Positive**

- Services evolve their schemas independently, with clear ownership.
- Strong local consistency (ACID) for each saga step.

**Negative / accepted trade-offs**

- No cross-service joins or transactions, by design (handled with sagas and read models).
- More databases to operate. Mitigated by managed PostgreSQL in Azure.
- Infrastructure code is PostgreSQL-specific (SQL in the outbox, `xmin`). Accepted:
  portability across databases is not a goal.

## Alternatives considered

- **Shared database, schema per service:** cheaper, but invites cross-schema queries and coupling.
- **SQL Server / Azure SQL:** an equally valid choice. PostgreSQL was chosen for being open
  source, available everywhere and offering `SKIP LOCKED` and `jsonb`.
- **Dapper for everything:** faster raw reads, but more hand-written mapping. EF Core projections
  are fast enough here, and Dapper can still be used behind `I*Queries` if a hot path needs it.
