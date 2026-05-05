# Data Sovereignty & Residency

## Regulatory Drivers

| Regulation | Scope | Key Requirement | Impact on Platform |
|---|---|---|---|
| GDPR | EU customers | Data must remain in EU | EU tenants: EU-only nodes |
| UK GDPR | UK customers | Post-Brexit residency | UK tenants: UK nodes or explicit transfer |
| CCPA | California consumers | Right to deletion | Soft-delete + purge workflow |
| PCI DSS | Cardholder data | No PAN storage | Tokenization (isolated segment) |
| LGPD | Brazil | Data in Brazil or explicit consent | BR region or DPA |
| RBI | India | Payment data in India | IN region for payment events |

---

## Tenant Geo-Fencing Model

```
Each tenant is assigned a PRIMARY REGION at onboarding.
Data (events, read models, projections) is written ONLY to the primary region.
Cross-region: only allowed for explicit DR (secondary region, same geo).

Tenant Config (Tenant Management Service):
  {
    "tenantId": "acme",
    "primaryRegion": "eu-west-1",     // Ireland
    "drRegion": "eu-central-1",       // Germany (same EU)
    "crossRegionReplication": true,
    "allowedGeos": ["EU"],
    "regulatoryRequirements": ["GDPR"],
    "dataResidencyClass": "strict"    // strict | relaxed | none
  }
```

---

## Architecture: Geo-Fenced Deployment

```
┌──────────────────────────────────────────────────────────┐
│  GLOBAL CONTROL PLANE (no customer data)                 │
│  Tenant registry, feature flags, routing config          │
│  Hosted: multi-region (no sovereignty constraint)        │
└────────────────────┬─────────────────────────────────────┘
                     │ Routing only (no data)
          ┌──────────┴──────────┐
          │                     │
┌─────────▼──────┐    ┌─────────▼──────┐
│   EU REGION    │    │   US REGION    │
│                │    │                │
│ EU tenants     │    │ US tenants     │
│ Event Store    │    │ Event Store    │
│ Read Models    │    │ Read Models    │
│ Kafka cluster  │    │ Kafka cluster  │
│ Redis          │    │ Redis          │
│                │    │                │
│ DR: eu-central │    │ DR: us-west    │
└────────────────┘    └────────────────┘
          │                     │
          │    NO CROSS-REGION  │
          │    DATA FLOW        │
          │                     │
┌─────────▼──────┐    ┌─────────▼──────┐
│  APAC REGION  │    │  UK REGION     │
│  (future)     │    │  (future)      │
└────────────────┘    └────────────────┘
```

---

## Event Replication Rules

```
ALLOWED replication:
  EU tenant → eu-west-1 (primary) → eu-central-1 (DR, same geo)
  US tenant → us-east-1 (primary) → us-west-2 (DR, same geo)

BLOCKED replication:
  EU tenant data MUST NOT replicate to US or APAC
  Gateway enforces this via Kafka topic ACLs (tenant-id partition key)
  Dapr pub/sub: topic per region, cross-region replication blocked

AUDIT:
  All replication events logged to immutable audit store
  GDPR violation alerts: CloudTrail/Azure Monitor anomaly detection
```

---

## GDPR Compliance Controls

```
Right to Erasure (Article 17):
  1. Customer submits erasure request
  2. Tenant admin triggers: DELETE /api/v1/admin/customers/{id}/erase
  3. Platform:
     a. Soft-delete customer record (tombstone event in event store)
     b. Queue hard-delete for read models (async, within 30 days)
     c. Anonymize event payload fields (name, email, card ref)
     d. Kafka: publish tombstone record to compact topics
     e. Audit log entry for erasure (retained for compliance)
  4. Cannot replay erased customer events without explicit re-consent

Right to Portability (Article 20):
  POST /api/v1/admin/customers/{id}/export
  Returns: JSON archive of all customer data (events + read models)
  Format: structured, machine-readable

Data Minimisation:
  Events contain only business-necessary fields
  No PII in Kafka message bodies by default
  PII in events: encrypted with tenant key (Dapr Vault)
```

---

## Disaster Recovery & Data Residency

```
DR Strategy:
  Primary region: full read-write
  DR region (same geo): Kafka MirrorMaker 2 + PostgreSQL streaming
  
  RTO: < 15 minutes (automated failover)
  RPO: < 30 seconds (Kafka replication lag + PG streaming)

DR Activation:
  1. Primary health check fails (3 consecutive, 30s interval)
  2. Traffic shifted to DR region via DNS failover
  3. DR PostgreSQL promoted to primary (pg_promote())
  4. Kafka MirrorMaker topology updated (DR cluster becomes source)
  5. Services restart with DR connection strings (Dapr secrets)

Data residency during DR:
  EU tenant DR: eu-central-1 (Germany) — still within EU ✅
  US tenant DR: us-west-2 — still within US ✅
  GDPR compliance maintained during DR activation
```
