# Multi-Tenancy Strategy

## Tenant Isolation Options

### Option A — Database-per-Tenant
- Each tenant has an isolated PostgreSQL database
- **Pros:** Strongest isolation, easy GDPR/compliance, independent scaling
- **Cons:** High infrastructure cost at scale (100s of tenants), migration complexity
- **Best For:** Large enterprise tenants, regulated industries

### Option B — Schema-per-Tenant (Chosen for this platform)
- Single PostgreSQL cluster; one schema per tenant
- **Pros:** Moderate isolation, shared infra cost, easier cross-tenant reporting
- **Cons:** Risk of cross-schema query mistakes, shared cluster limits
- **Best For:** Mid-market SaaS with dozens–hundreds of tenants

### Option C — Row-Level-Security (Hybrid)
- Single schema, `tenant_id` column + PostgreSQL RLS policies
- **Pros:** Minimal infra, lowest cost, elastic
- **Cons:** Weaker isolation, RLS bugs can expose data, harder to shard
- **Best For:** High-volume small tenants (thousands)

## This Platform: Schema-per-Tenant + RLS Fallback

```
┌──────────────────────────────────────────────────────┐
│               PostgreSQL Cluster                      │
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐  │
│  │ tenant_acme │  │ tenant_beta │  │ tenant_corp │  │
│  │  (schema)   │  │  (schema)   │  │  (schema)   │  │
│  └─────────────┘  └─────────────┘  └─────────────┘  │
└──────────────────────────────────────────────────────┘
```

## Tenant Context Propagation

```
HTTP Request
  │  X-Tenant-Id: acme
  │  X-Store-Id:  store-42
  │  X-Terminal-Id: terminal-07
  ▼
TenantMiddleware
  │  Resolves ITenantContext (scoped DI)
  ▼
MediatR Pipeline
  │  TenantBehavior<TRequest, TResponse>
  │    - Validates tenant context
  │    - Sets DB schema search_path
  ▼
Command/Query Handler
  │  ITenantContext injected
  ▼
Domain Event
  │  EventMetadata.TenantId = tenantId
  ▼
Kafka Message
  │  Header: tenant-id: acme
  ▼
Consumer
  │  Reads tenant from message header
  │  Re-resolves ITenantContext
  ▼
Projection / Read Model
     DB schema = tenant schema
```
