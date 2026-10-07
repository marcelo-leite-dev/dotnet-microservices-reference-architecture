# ADR-0008: Redis cache-aside for catalog reads

- **Status:** Accepted
- **Date:** 2026-10-05

## Context

Product reads (`GET /api/products/{id}`) vastly outnumber writes in a catalog. Every read hitting
PostgreSQL wastes database capacity and adds latency. With several replicas, a per-instance
in-memory cache would serve inconsistent data across instances after a change.

## Decision

Use a **distributed Redis cache** with the **cache-aside** pattern for single-product reads:

1. Read from Redis. On a hit, return.
2. On a miss, project from PostgreSQL, write to Redis with a **TTL** (default 10 minutes), and return.
3. Every command that changes a product (price change, restock, stock reservation) **removes**
   the key after committing.

Implementation rules:

- The cache sits behind the `IProductCache` port. The application layer doesn't know about Redis.
- **Fail open:** cache errors are logged and treated as misses. Redis being down must not make
  the catalog unavailable.
- Instrumentation: `catalog.cache.requests{result=hit|miss|error}` and Redis spans in traces.
- Batch reads used by Ordering (`/api/products/batch`) are **not** cached: pricing an order
  should see the latest committed price.

## Consequences

**Positive**

- Lower read latency and database load, shared across all replicas.
- Simple mental model: the database is the source of truth and the cache is disposable.

**Negative / accepted trade-offs**

- **Bounded staleness:** a reader can see an old value between the commit and the invalidation,
  or for up to the TTL if an invalidation is lost. Acceptable for catalog browsing. Prices that
  matter (pricing an order) bypass the cache.
- A stampede on a hot key after expiry is possible. Mitigation if needed: request coalescing,
  or `HybridCache` once on .NET 9+ (see ADR-0012).

## Alternatives considered

- **In-memory cache per instance:** fastest, but inconsistent across replicas.
- **Write-through cache:** keeps the cache always fresh, but couples every write to Redis
  availability, which conflicts with fail-open.
- **HTTP response caching / CDN:** useful at the edge, but blind to domain invalidation events.
