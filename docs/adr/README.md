# Architecture Decision Records

An **Architecture Decision Record (ADR)** captures one significant decision: the context that
forced it, the option chosen, the alternatives rejected and the consequences accepted. ADRs are
immutable once accepted. When a decision changes, a new ADR **supersedes** the old one, so the
history of *why* the system looks the way it does is never lost.

Format: a lightweight variant of [MADR](https://adr.github.io/madr/). New records start from
[`0000-template.md`](0000-template.md).

| # | Decision | Status |
| --- | --- | --- |
| [0001](0001-record-architecture-decisions.md) | Record architecture decisions | Accepted |
| [0002](0002-clean-architecture-per-service.md) | Clean Architecture inside each service | Accepted |
| [0003](0003-cqrs-with-in-house-dispatcher.md) | CQRS with an in-house dispatcher instead of MediatR | Accepted |
| [0004](0004-event-driven-integration-with-kafka.md) | Event-driven integration over Apache Kafka | Accepted |
| [0005](0005-choreography-saga-for-order-processing.md) | Choreography-based saga for order processing | Accepted |
| [0006](0006-transactional-outbox-and-idempotent-consumers.md) | Transactional outbox and idempotent consumers | Accepted |
| [0007](0007-database-per-service-with-postgresql.md) | Database per service on PostgreSQL with EF Core | Accepted |
| [0008](0008-redis-cache-aside-for-catalog-reads.md) | Redis cache-aside for catalog reads | Accepted |
| [0009](0009-observability-with-opentelemetry-elk-and-grafana.md) | Observability with OpenTelemetry, ELK and Grafana | Accepted |
| [0010](0010-testing-strategy-with-testcontainers.md) | Testing strategy: unit, architecture and Testcontainers | Accepted |
| [0011](0011-ci-cd-with-github-actions-and-aks.md) | CI/CD with GitHub Actions and deployment to AKS | Accepted |
| [0012](0012-target-dotnet-8-lts.md) | Target .NET 8 LTS, with a planned move to .NET 10 | Accepted |
