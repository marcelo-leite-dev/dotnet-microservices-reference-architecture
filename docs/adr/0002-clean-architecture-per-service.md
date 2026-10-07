# ADR-0002: Clean Architecture inside each service

- **Status:** Accepted
- **Date:** 2026-10-05

## Context

Each service contains business rules (stock can't go negative, an order moves
`Pending → Confirmed | Rejected`) and infrastructure concerns (PostgreSQL, Kafka, Redis, HTTP).
When the two are mixed, business rules can only be tested through the database, and replacing a
technology means touching use cases. A reference architecture must also make its structure
obvious to a reader.

## Decision

Every service is split into four projects with dependencies pointing **inwards only**:

| Project | Contains | References |
| --- | --- | --- |
| `*.Domain` | Aggregates, invariants, domain events, repository interfaces | `BuildingBlocks.Domain` |
| `*.Application` | Commands, queries, handlers, validators, ports (`ICatalogClient`, `IProductCache`, ...) | Domain, contracts |
| `*.Infrastructure` | EF Core, Kafka, Redis, HTTP clients: implementations of the ports | Application |
| `*.Api` | Endpoints and the composition root (DI wiring) | Infrastructure, `BuildingBlocks.Web` |

Additional rules:

- Services couple to each other **only** through `*.Contracts` assemblies, which contain only event records.
- Handlers are `internal sealed`: the public surface of the application layer is its commands and queries.
- Use cases are organized as **vertical slices**: one file holds a command, its validator and its handler.
- The rules are **enforced by tests** (`tests/ArchitectureTests`, using NetArchTest), so a
  violation breaks the build.

## Consequences

**Positive**

- Domain and application logic are unit-tested in milliseconds without infrastructure.
- Infrastructure can be replaced (e.g. Kafka with Azure Service Bus) by adding an adapter.
- The structure is identical in every service, so moving between them is cheap.

**Negative / accepted trade-offs**

- More projects and some mapping code than a single-project service.
  Accepted: the services are small and the layers are thin.
- The domain uses EF Core-friendly constructs (private parameterless constructors, private
  setters). Accepted as a pragmatic compromise instead of a separate persistence model.

## Alternatives considered

- **Single project per service with folders:** fewer files, but dependency rules can't be
  enforced by the compiler or by tests.
- **Vertical slice architecture without layers:** great for CRUD-heavy services, but it makes
  the domain model less explicit, and the domain model is the point here.
