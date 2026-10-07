# ADR-0003: CQRS with an in-house dispatcher instead of MediatR

- **Status:** Accepted
- **Date:** 2026-10-05

## Context

Reads and writes have different needs. Writes must load aggregates and enforce invariants.
Reads want flat, fast projections, often cached. Every use case also needs the same
cross-cutting behavior: validation, tracing, metrics and logging.

The usual .NET answer is MediatR. Since 2025, new versions of MediatR require a commercial license
for many organizations, and the library does much more than this system needs.

## Decision

1. Apply **CQRS at the application level** (one database, two models):
   - Commands (`ICommand<T>`) go through repositories and aggregates and commit via `IUnitOfWork`.
   - Queries (`IQuery<T>`) use dedicated read services (`IProductQueries`, `IOrderQueries`) that
     project with `AsNoTracking()` straight into DTOs, bypassing the domain model.
2. Route both through a small **in-house dispatcher** (`BuildingBlocks.Application/Cqrs`)
   with **pipeline behaviors**:
   - `InstrumentationBehavior` (outermost): an OpenTelemetry span, the `app.request.duration` histogram, and failure logging.
   - `ValidationBehavior`: runs FluentValidation validators and throws `ValidationException`.
3. Handlers, domain event handlers and validators are registered by assembly scanning.

## Consequences

**Positive**

- Every use case gets validation and observability without any code in the handler.
- Read paths can be optimized (projections, caching) without compromising the write model.
- No third-party licensing risk. The dispatcher is about 60 lines and fully understood.

**Negative / accepted trade-offs**

- The dispatcher must be maintained in-house. Mitigated by its small size and unit tests
  (`DispatcherTests`).
- An extra indirection between endpoint and handler. Accepted for the uniform pipeline.
- Reads and writes share a database, so CQRS here does not provide independent read scaling.
  A separate read store can be introduced later behind the same `I*Queries` ports.

## Alternatives considered

- **MediatR:** feature-rich and familiar, but commercially licensed in recent versions and more
  than needed.
- **Calling handlers directly from endpoints:** simplest, but cross-cutting concerns would be
  duplicated or need decorators registered per handler.
- **Full CQRS with a separate read database:** justified only when read load or read shape
  diverges strongly. It's premature for this domain.
