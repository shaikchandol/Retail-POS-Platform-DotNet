# ADR-007: Offline-First Store Architecture

**Status:** Accepted  
**Date:** 2025-01  
**Deciders:** Platform Architecture Board, Retail Operations  

---

## Context

Retail stores experience intermittent connectivity. Prior system required
continuous internet connection — store operations halted during outages,
causing significant revenue loss (average: 2.3 hours/month per store).

## Decision

Implement offline-first architecture:

1. **SQLite local event buffer** on each store server / POS cluster
2. **StoreAndForwardWorker** (ASP.NET Core hosted service) syncs on connectivity restore
3. **EMV offline card approval** for transactions under floor limit ($100)
4. **Priority queue**: Sales > Payments > Inventory > Telemetry
5. **Adaptive sync**: bandwidth-aware batching strategy
6. **Conflict resolution rules** per event type (server-wins for pricing, store-wins for sales)

## Consequences

**Positive:**
- Store operations continue 100% during network outages
- PCI-compliant offline card approval (EMV chip-based)
- No data loss — all events buffered and eventually synced
- Bandwidth optimization reduces costs for low-connectivity stores

**Negative:**
- Eventual consistency — cloud read models lag behind store reality during offline periods
- Settlement risk for offline card approvals above floor limit (managed by acquirer)
- Conflict resolution requires careful governance (especially inventory oversell)
- Local storage adds operational complexity (buffer monitoring, disk space alerts)

## Alternatives Rejected

| Alternative | Reason Rejected |
|---|---|
| Require always-online | Unacceptable business risk — store revenue loss |
| Satellite backup link | High cost; doesn't eliminate need for local buffering |
| Cloud-only with retry | Retry doesn't help during extended outages |
