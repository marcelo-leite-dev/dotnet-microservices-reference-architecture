# ADR-0006: Transactional outbox and idempotent consumers

- **Status:** Accepted
- **Date:** 2026-10-05

## Context

Every saga step must **change state and publish an event atomically**. The database and Kafka
share no transaction, which creates the *dual-write problem*:

- commit then publish: a crash in between **loses** the event;
- publish then commit: a failed commit publishes an event about something that **never happened**.

On the consuming side, Kafka (like any broker) delivers **at least once**: rebalances, crashes
before the offset commit and producer retries all cause redelivery. Processing a reservation
twice would decrement stock twice.

## Decision

**Transactional outbox (producer side)**

- Integration events are written to an `outbox_messages` table **in the same transaction** as the
  business change (`IOutbox.Enqueue`, called from domain event handlers before `SaveChanges`).
- A background `OutboxProcessor` relays rows to Kafka and marks them processed:
  - claims rows with `SELECT ... FOR UPDATE SKIP LOCKED`, so multiple replicas are safe;
  - stops a batch at the first failure to preserve ordering, recording `attempts` and `last_error`;
  - uses a partial index `WHERE processed_on_utc IS NULL` to keep polling cheap;
  - stores the originating `traceparent`, so traces continue across the hop.

**Idempotent consumer / inbox (consumer side)**

- For each message: open a transaction, check `inbox_messages (message_id, consumer)`, run the
  handler, insert the inbox row, commit, and **only then** commit the Kafka offset.
- Transient failures are retried with exponential backoff. After `Kafka:MaxProcessingAttempts`,
  the message goes to `{topic}.dlq` with the reason in headers, so the partition is not blocked.
- Domain operations are also idempotent where natural (`Order.Confirm` twice is a no-op), as a
  second line of defense.

## Consequences

**Positive**

- No lost or phantom events. Delivery is at-least-once and processing is effectively-once.
- The pattern is generic and lives in `BuildingBlocks.Infrastructure`: services just call `IOutbox`.

**Negative / accepted trade-offs**

- Up to one polling interval (default 1 s) of added latency.
- Outbox and inbox tables grow. A retention job (delete processed rows older than N days) is on
  the roadmap.
- The `FOR UPDATE SKIP LOCKED` query is PostgreSQL-specific (fine under ADR-0007).
- Side effects outside the database (e.g. sending an email) are not covered by the inbox
  transaction and must be idempotent on their own.

## Alternatives considered

- **Change Data Capture (Debezium) on the outbox table:** lower latency and no polling, but it
  adds Kafka Connect to operate. It is the natural evolution at higher scale.
- **Kafka transactions (exactly-once semantics):** only cover Kafka-to-Kafka flows. They can't
  make a PostgreSQL write and a Kafka publish atomic.
- **Publishing directly and accepting rare loss:** unacceptable for stock and orders.
