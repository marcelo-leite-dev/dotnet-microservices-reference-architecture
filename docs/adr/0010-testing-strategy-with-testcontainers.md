# ADR-0010: Testing strategy: unit, architecture and Testcontainers

- **Status:** Accepted
- **Date:** 2026-10-05

## Context

The most important behaviors of this system live at the boundaries: `FOR UPDATE SKIP LOCKED`,
`xmin` concurrency, EF Core migrations, Kafka headers and offsets, Redis TTLs, idempotent
consumption. In-memory substitutes (EF Core InMemory, fake brokers) don't implement these
behaviors, so tests against them prove little. At the same time, business rules need fast
feedback that doesn't depend on Docker.

## Decision

Three complementary test layers:

| Layer | Tooling | Scope | Runs |
| --- | --- | --- | --- |
| **Unit** | xUnit, NSubstitute | Aggregates and state machines; handlers with substituted ports; dispatcher pipeline | every build, in ms |
| **Architecture** | NetArchTest | Dependency rules of ADR-0002; services couple only via contracts; handler visibility | every build, in ms |
| **Integration** | `WebApplicationFactory` + **Testcontainers** (PostgreSQL, Kafka, Redis) | The real API and real infrastructure: HTTP → DB → outbox → Kafka → consumer → DB | CI and locally with Docker |

Conventions:

- Integration tests are tagged `[Trait("Category", "Integration")]`, so CI runs them as a separate
  stage and developers can run the fast suite alone.
- Containers are shared per test collection (`ICollectionFixture`) and test data is isolated by
  unique ids, which keeps the suite fast without cross-test interference.
- Only **other services** are faked in integration tests (e.g. `FakeCatalogClient` in Ordering).
  Each service's own infrastructure is always real.
- Asynchronous outcomes are asserted by polling (`Eventually`) or by consuming the expected event
  from Kafka. There are no fixed `Thread.Sleep` calls.

## Consequences

**Positive**

- High confidence in exactly the behaviors that break in production.
- Fast inner loop thanks to unit and architecture tests, with no Docker needed for them.

**Negative / accepted trade-offs**

- Integration tests need Docker and take tens of seconds to start containers.
- Asynchronous tests can be timing-sensitive. Mitigated with generous timeouts and polling
  instead of sleeps.

## Alternatives considered

- **EF Core InMemory / SQLite:** fast, but semantically different from PostgreSQL.
- **A shared, long-lived test environment:** flaky (shared state) and slow feedback.
- **Consumer-driven contract tests (Pact) between services:** valuable as the number of services
  and teams grows, and a candidate future addition for the HTTP contract between Ordering and Catalog.
