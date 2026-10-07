# ADR-0005: Choreography-based saga for order processing

- **Status:** Accepted
- **Date:** 2026-10-05

## Context

Placing an order spans two databases: Ordering stores the order and Catalog owns the stock.
A distributed transaction (2PC) would couple both databases' availability, isn't supported
by Kafka or by most managed databases, and doesn't scale. Yet the business needs a consistent
outcome: either the stock is reserved and the order confirmed, or nothing is reserved and the
order is rejected.

## Decision

Model order processing as a **saga**: a sequence of local transactions linked by events, with
**choreography** (no central coordinator).

| Step | Service | Local transaction | Emits |
| --- | --- | --- | --- |
| 1 | Ordering | Price via Catalog snapshot, insert order as `Pending` | `OrderPlaced` |
| 2 | Catalog | Check **all** items, then decrement stock **all or nothing** | `StockReserved` or `StockReservationFailed` |
| 3 | Ordering | `Confirm` or `Reject` (with reason) | `OrderConfirmed` or `OrderRejected` |

Rules:

- Each step is one local ACID transaction that includes its outgoing event (outbox, ADR-0006).
- Steps are **idempotent**: `Order.Confirm` on a confirmed order is a no-op, and inbox
  de-duplication protects stock reservation.
- Catalog validates every item before changing any, so a failed reservation needs no compensation.
- The HTTP API returns **202 Accepted** with a `Location` header, and clients poll the order status.

## Consequences

**Positive**

- No distributed locks or transactions. Each service stays autonomous.
- Easy to extend: a new service (e.g. Payments) can join by subscribing to existing events.

**Negative / accepted trade-offs**

- The end-to-end flow is **implicit** (spread across handlers). Mitigated by the
  sequence diagram in the README and by distributed tracing, which shows the saga as one trace.
- Intermediate states are visible (`Pending`).
- Choreography gets harder to follow as steps grow. Once payment, shipping and cancellation
  with stock release are added, this ADR should be revisited in favor of an orchestrator.

## Alternatives considered

- **Orchestration** (a central saga state machine, e.g. MassTransit sagas or Temporal): explicit
  flow, timeouts and compensation in one place, but it adds a coordinating component and
  framework. That is not justified for three steps.
- **Two-phase commit:** rejected for the coupling and availability reasons above.
- **Synchronous reservation over HTTP during placement:** simpler UX (immediate answer), but
  couples Ordering's availability to Catalog's and still risks inconsistency on partial failures.
