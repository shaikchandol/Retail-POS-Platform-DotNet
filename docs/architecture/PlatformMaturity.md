# Platform Maturity Model — L1 to L5

## Overview

The platform is designed to evolve through 5 maturity levels. Each level is
independently deployable — teams advance through levels at their own pace.
Current target: **L3** (Scalable). L4 activatable per tenant.

---

## L1 — Basic

**Definition:** Single-region, minimal automation, limited observability.

```
Capabilities:
✅ Single microservice deployed
✅ Basic CI (build + test)
✅ Manual deployment
✅ Single PostgreSQL DB
✅ Basic logging (console)
✅ Single tenant

Missing:
❌ Event sourcing
❌ Kafka
❌ Multi-tenancy
❌ Observability
❌ Automated DR

Target users: PoC, hackathon, initial MVP
```

---

## L2 — Standardised

**Definition:** CI/CD pipeline, basic observability, standard service templates.

```
Capabilities:
✅ All L1 capabilities
✅ Dapr sidecar (pub/sub, state, secrets)
✅ Basic CQRS (commands + queries)
✅ Azure DevOps pipeline (build → test → deploy)
✅ Podman containerization
✅ OpenTelemetry traces (basic)
✅ Prometheus + Grafana (service metrics)
✅ Schema-per-tenant isolation
✅ JWT authentication

Missing:
❌ Full event sourcing
❌ Multi-region
❌ Feature flags
❌ SLOs
❌ Zero-trust networking

Target users: staging environment, early production
```

---

## L3 — Scalable ← CURRENT TARGET

**Definition:** Multi-team, event-driven, SLOs, feature flags, Kafka.

```
Capabilities:
✅ All L2 capabilities
✅ Full CQRS + Event Sourcing (all aggregates)
✅ Apache Kafka (Strimzi) — durable event backbone
✅ Choreography sagas (event-driven workflows)
✅ Tenant-aware feature flags
✅ Per-tenant SLO measurement
✅ Zero-trust networking (Dapr mTLS, NetworkPolicy)
✅ Testcontainers integration tests
✅ HPA (autoscaling per service)
✅ BFF pattern (3 BFFs)
✅ API Gateway with Policy-as-Code

Missing:
❌ Multi-region deployment
❌ Orchestration sagas
❌ Offline-first
❌ FinOps cost attribution
❌ Progressive delivery (canary/blue-green)

Target users: production, multi-team development
```

---

## L4 — Resilient

**Definition:** Multi-region, progressive delivery, zero-trust, FinOps.

```
Capabilities:
✅ All L3 capabilities
✅ Multi-region deployment (active/passive)
✅ Kafka MirrorMaker 2 (cross-region replication)
✅ Blue-green deployments (via K8s + YARP)
✅ Canary releases (tenant-scoped)
✅ Orchestration sagas (Dapr Workflow)
✅ Retail offline-first (StoreAndForwardWorker)
✅ FinOps cost attribution (per-tenant API/event/storage metrics)
✅ PCI tokenization service (isolated network segment)
✅ Automated DR testing (chaos engineering)
✅ Secret rotation (Dapr + Vault)
✅ Data sovereignty (geo-fenced tenants)

Missing:
❌ Predictive autoscaling
❌ Continuous architecture governance
❌ Tenant-level economics (true chargeback)

Target users: enterprise production, regulated industries
```

---

## L5 — Optimised

**Definition:** Tenant-level economics, predictive scaling, continuous optimisation.

```
Capabilities:
✅ All L4 capabilities
✅ Predictive autoscaling (ML-driven, based on demand forecast)
✅ True tenant chargeback (billing per API call / event / GB)
✅ Self-healing infrastructure (automated anomaly response)
✅ Continuous architecture governance (ADR compliance checks)
✅ Architecture fitness functions (ArchUnit-style)
✅ AI-driven cost optimisation (right-sizing recommendations)
✅ Capacity planning automation
✅ Continuous compliance (PCI, GDPR automated evidence)
✅ Full platform engineering (developer self-service, golden paths)

Target users: platform-as-a-product teams, ISV-scale operations
```

---

## Capability Roadmap

```mermaid
gantt
    title Platform Maturity Roadmap
    dateFormat YYYY-QQ

    section L1 Basic
    Initial deployment       :done, 2024-Q1, 2024-Q2

    section L2 Standardised
    CI/CD + Dapr + CQRS      :done, 2024-Q2, 2024-Q3

    section L3 Scalable
    Event Sourcing + Kafka   :done, 2024-Q3, 2025-Q1
    Feature Flags + SLOs     :done, 2024-Q4, 2025-Q1
    Zero-Trust + BFF + GW    :active, 2025-Q1, 2025-Q2

    section L4 Resilient
    Multi-region             :2025-Q2, 2025-Q3
    Offline-First + Sagas    :2025-Q2, 2025-Q3
    FinOps + PCI             :2025-Q3, 2025-Q4

    section L5 Optimised
    Predictive Scaling + ML  :2025-Q4, 2026-Q2
    Full FinOps Chargeback   :2026-Q1, 2026-Q3
```

---

## Maturity Assessment Checklist

| Capability | L1 | L2 | L3 | L4 | L5 |
|---|---|---|---|---|---|
| CI/CD pipeline | - | ✅ | ✅ | ✅ | ✅ |
| CQRS | - | Basic | Full ES | Full ES | Full ES |
| Event backbone | - | - | Kafka | Kafka | Kafka |
| Observability | Logs | OTel | Full | Full | AI-driven |
| Multi-tenancy | - | Schema | Schema+FF | Geo+RLS | Full econ |
| Security | JWT | JWT+Dapr | ZT+mTLS | PCI+Vault | Continuous |
| Deployment | Manual | CD | CD+Flags | Canary | Predictive |
| Testing | Unit | +Integration | +Contract | +Chaos | +Fitness |
| Resilience | Single | Circuit CB | Saga | Multi-region | Self-heal |
| Cost | None | None | SLO | FinOps | Chargeback |
