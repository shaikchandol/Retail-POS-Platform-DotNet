# ADR-006: Backend For Frontend (BFF) Pattern

**Status:** Accepted  
**Date:** 2025-01  
**Deciders:** Platform Architecture Board, POS Team, Web Team, Mobile Team  

---

## Context

Three distinct client types (POS terminal, web manager portal, customer mobile app)
have fundamentally different data shape requirements:

- **POS terminal**: minimal payload, low latency, offline-resilient, one-call checkout
- **Web portal**: rich aggregated dashboards, multi-table joins, response caching
- **Mobile app**: compressed payloads, push notification integration, loyalty context

A single API cannot optimally serve all three without excessive coupling.

## Decision

Implement three independent BFF services, each an ASP.NET Core Web project:

- `RetailPos.Bff.Pos` — owned by POS team
- `RetailPos.Bff.Web` — owned by web portal team
- `RetailPos.Bff.Mobile` — owned by mobile team

Each BFF:
- Owns its own aggregation logic and DTO shapes
- Can evolve independently of the domain API
- Contains NO domain business logic (pure aggregation/shaping)
- Has its own deployment cadence

## Consequences

**Positive:**
- Client teams move at their own pace
- Optimal payload shapes per client (no over-fetching, no under-fetching)
- BFF contains client-specific caching (POS: product cache; Web: dashboard cache)
- Clear ownership boundaries

**Negative:**
- Three additional deployable units to operate
- Risk of duplicating orchestration logic across BFFs (mitigated by shared building blocks)
- BFF can become a "dumping ground" for logic — governance required

## Governance Rules

1. BFFs call downstream APIs only — no direct DB access
2. BFFs do not contain domain objects — only view/response models
3. Each BFF has < 2,000 lines of code — extract to service if exceeded
4. BFFs must not add business invariants — those belong in domain services
