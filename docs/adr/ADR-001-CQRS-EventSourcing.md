# ADR-001: CQRS + Event Sourcing for Business Aggregates

**Date:** 2025-05-05  
**Status:** Accepted  
**Deciders:** Platform Architecture Team  

## Context

The Retail POS platform handles high-volume, low-latency transaction processing (checkout) while also needing complex read patterns (sales dashboards, inventory reports, audit trails). Traditional CRUD with a single mutable table does not satisfy:
- Full audit trail requirement (regulatory compliance)
- Time-travel queries ("what was the inventory at 14:00 last Tuesday?")
- Event-driven integration (other services need to react to sales events)
- Conflict resolution under concurrent updates

## Decision

Apply **CQRS** for all write-heavy aggregates (Sales, Orders, Payments, Inventory) with **Event Sourcing** as the persistence mechanism.

**Write side:** Aggregates persist domain events to an append-only event store (PostgreSQL).  
**Read side:** Separate denormalized read models, updated asynchronously via Kafka event projections.

## Consequences

**Positive:**
- Complete, immutable audit trail of all business state changes
- Temporal queries: replay events to any point in time
- Read models optimized per use case (no compromise between write and read schemas)
- Natural fit for event-driven architecture (events are the integration mechanism)
- Independent scaling of read and write workloads

**Negative:**
- Higher complexity: two models, eventual consistency to explain to teams
- Projection delays: read model lags behind write (typically < 100ms on Kafka)
- Event stream management: schema evolution requires care
- Debugging complexity: must understand event replay to reason about state

## Alternatives Considered

| Option | Rejected Reason |
|---|---|
| CRUD only | No audit trail, complex queries compromise write schema |
| CQRS without ES | Loses audit trail and time-travel, events derived post-hoc |
| ES with single DB | No horizontal scaling of reads, tight coupling |

## Notes

- **Snapshot policy:** Aggregate streams > 500 events get periodic snapshots
- **Retention:** Event store kept indefinitely; Kafka topics retain 30 days (sales)
- **Read lag SLA:** < 500ms from command to read model update under normal load
