# FinOps — Tenant-Level Cost Attribution

## Cost Attribution Model

Every API call, event, and byte stored is measured and attributed to a tenant.
This enables showback (reporting costs) and, at L5 maturity, chargeback (billing).

---

## Cost Dimensions

```
┌─────────────────────────────────────────────────────────────────┐
│              TENANT COST ATTRIBUTION DIMENSIONS                 │
│                                                                 │
│  1. COMPUTE (API Calls)                                         │
│     Metric: retailpos.api.calls{tenant_id, service, operation} │
│     Cost driver: CPU-ms per request (profiled per endpoint)    │
│                                                                 │
│  2. MESSAGING (Event Throughput)                                │
│     Metric: retailpos.events.published{tenant_id, topic}       │
│     Cost driver: Kafka GB-in + GB-out per tenant               │
│                                                                 │
│  3. STORAGE (Database Growth)                                   │
│     Metric: pg_database_size(tenant_{id})                      │
│     Cost driver: PostgreSQL GB per tenant schema               │
│                                                                 │
│  4. CACHE (Redis Usage)                                         │
│     Metric: redis.keyspace.used{prefix=tenant_id}              │
│     Cost driver: Redis memory-hours per tenant                 │
│                                                                 │
│  5. BANDWIDTH (Offline Sync)                                    │
│     Metric: retailpos.sync.bytes{tenant_id}                    │
│     Cost driver: GB transferred for store sync                 │
└─────────────────────────────────────────────────────────────────┘
```

---

## Showback Dashboard (Grafana)

```
Per-Tenant Cost Report (monthly view):

TENANT: ACME RETAIL
────────────────────────────────────────────────────────────────
API Calls:         2,400,000 calls    $48.00   (at $0.02/1k)
Event Throughput:  45 GB              $45.00   (at $1.00/GB)
DB Storage:        120 GB             $24.00   (at $0.20/GB)
Redis Cache:       4 GB-months        $8.00    (at $2.00/GB)
Offline Sync:      8 GB               $4.00    (at $0.50/GB)
────────────────────────────────────────────────────────────────
TOTAL ESTIMATED:                      $129.00
Subscription Plan:                    Professional ($200/month)
Platform Margin:                      $71.00 (35.5%)
────────────────────────────────────────────────────────────────
```

---

## Architecture-Driven Cost Levers

| Lever | Mechanism | Typical Saving |
|---|---|---|
| Read model caching | Redis cache for hot queries | -40% compute |
| Event batching | Batch Kafka publishes (100 events) | -60% Kafka cost |
| Snapshot strategy | Avoid replaying 10k events per request | -80% event store reads |
| Connection pooling | PgBouncer — fewer DB connections | -30% DB cost |
| Adaptive sync BW | Priority queue for offline sync | -50% bandwidth |
| HPA min replicas | Set to 1 in off-peak (dev/test) | -70% off-peak compute |
| Cold tier storage | Move event store > 90 days to S3/Blob | -90% old event storage |

---

## FinOps Maturity Progression

| Level | Capability |
|---|---|
| L3 | SLO measurement per tenant (no cost attribution) |
| L4 | Showback — cost report per tenant, no billing |
| L5 | Chargeback — automated billing per tenant based on usage |

## Cost Anomaly Detection

```
SLO Worker (SloEvaluationWorker) also tracks:
  - Tenant cost spike: > 3x previous 7-day average
  - Alerts: OpsTeam + TenantAdmin notification
  - Automated: throttle aggressive tenants to protect platform

Metric: retailpos.tenant.cost.daily{tenant_id}
Alert threshold: configurable per subscription tier
```
