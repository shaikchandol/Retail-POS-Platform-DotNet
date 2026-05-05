# ADR-003: Multi-Tenancy Strategy — Schema-per-Tenant

**Date:** 2025-05-05  
**Status:** Accepted  

## Context

The platform must serve multiple retail tenants (e.g., 50–500 mid-market retailers) with strong data isolation, reasonable cost efficiency, and the ability to apply per-tenant configuration (pricing rules, tax rates, promotions).

## Decision

**Schema-per-Tenant** on a shared PostgreSQL Flexible Server cluster.

Each tenant gets:
- A dedicated PostgreSQL schema (e.g., `tenant_acme`, `tenant_beta`)
- Redis keyspace prefix (`acme:*`, `beta:*`)
- Kafka header isolation (`tenant-id: acme`)
- Dapr state store key prefix per tenant

**Tenant context propagation:**
1. HTTP: `X-Tenant-Id` header or JWT `tenant_id` claim
2. `TenantMiddleware` populates `ITenantContext` (scoped DI)
3. `TenantValidationBehavior` (MediatR pipeline) validates before every handler
4. EF Core: `SET search_path = tenant_{id}` at connection open
5. Kafka events: `tenant-id` message header
6. Dapr state: key prefix `{tenantId}|{key}`

## Consequences

**Positive:**
- Strong logical isolation per tenant (schema boundary)
- Moderate cost — single cluster shared infrastructure
- Easy per-tenant migrations (apply to one schema at a time)
- GDPR compliance: delete tenant schema = complete data erasure

**Negative:**
- Shared cluster is a single point of failure (mitigated by HA config)
- Noisy-neighbor risk: one tenant's heavy queries can affect others
- Connection pooling: PgBouncer needed to manage connections at scale

## Trade-off Analysis

| Strategy | Isolation | Cost | Complexity | Scale |
|---|---|---|---|---|
| DB-per-tenant | Highest | Highest | High | Limited |
| **Schema-per-tenant (chosen)** | **High** | **Medium** | **Medium** | **Good** |
| Row-level-security | Low | Lowest | Low | Excellent |
| Hybrid (large=DB, small=schema) | Adaptive | Adaptive | Highest | Best |

## Escalation Path

- > 500 tenants or large enterprise tenant: migrate to DB-per-tenant
- > 10,000 tenants (SMB): add RLS on top of schema isolation
- Current: schema-per-tenant covers the 50–500 target range
