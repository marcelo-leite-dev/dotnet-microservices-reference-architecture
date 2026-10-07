# ADR-0004: Event-driven integration over Apache Kafka

- **Status:** Accepted
- **Date:** 2026-10-05

## Context

Catalog and Ordering must react to each other's state changes (an order needs stock reserved,
and a reservation outcome changes the order). Synchronous HTTP chains would couple their
availability: if Catalog is down, no order could be placed or completed. We need asynchronous,
durable and ordered communication that tolerates consumers being offline.

## Decision

Services publish **integration events** to **Apache Kafka** and react to each other's events.

- **Contracts:** each service publishes its events in a `*.Contracts` assembly containing
  immutable records with primitive types only. Consumers reference contracts, never internals.
  Contracts evolve **additively** (new optional fields), and breaking changes require a new event type.
- **Topics per aggregate stream**, with several event types per topic:
  `ordering.orders`, `catalog.stock-reservations`, `catalog.products`.
- **Message key = aggregate id**, so all events of one aggregate land on the same partition and
  are consumed **in order**, while different aggregates are processed in parallel.
- **Headers:** `event-type` (routing), `message-id` (de-duplication, see ADR-0006) and W3C
  `traceparent` (distributed tracing, see ADR-0009).
- **Producer:** `acks=all` and `enable.idempotence=true`.
- **Consumer:** manual offset commit after successful processing, retries with backoff, and a
  per-topic dead-letter topic `{topic}.dlq` for poison messages.
- Domain events (in-process) and integration events (cross-service) are **distinct types**. A
  domain event handler decides what becomes public (see ADR-0006).
- The transport is hidden behind `BuildingBlocks.Messaging` abstractions (`IOutbox`,
  `IIntegrationEventHandler<T>`), so application code never references Confluent.Kafka.

## Consequences

**Positive**

- Temporal decoupling: a service can be down while others keep accepting work. Events wait
  in Kafka.
- Kafka's retained log allows replay, new consumers and auditing.
- Per-key ordering without global ordering costs.

**Negative / accepted trade-offs**

- **Eventual consistency.** Clients see `Pending` orders, and the API makes this explicit with 202 Accepted.
- Kafka is operationally heavy. Mitigated by a managed offering in Azure (Event Hubs Kafka
  endpoint) and a single KRaft node locally.
- At-least-once delivery, so consumers must be idempotent (ADR-0006).
- Compile-time contract sharing couples release coordination of contract changes. A schema
  registry (Avro/Protobuf) is the next step if more teams or languages join.

## Alternatives considered

- **RabbitMQ:** excellent for work queues and routing, but it has no durable replayable log and
  weaker per-key ordering semantics at scale.
- **Azure Service Bus:** a strong managed option with sessions for ordering. Rejected to stay
  cloud-agnostic and because Kafka skills are broadly transferable. The abstraction keeps the
  door open.
- **Synchronous HTTP between services:** simpler, but couples availability and latency, and
  needs distributed transactions or ad-hoc compensation anyway.
