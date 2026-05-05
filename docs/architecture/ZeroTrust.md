# Zero-Trust Networking Architecture

## Core Principle

> **Never trust, always verify.** No implicit trust based on network location.
> Every request is authenticated, authorized, and encrypted — regardless of origin.

## 3D Network Model: Edge → Mesh → Core

```
                    NORTH-SOUTH (External Traffic)
                    ┌────────────────────────────────────────┐
                    │          EDGE LAYER                    │
                    │  ┌──────────────────────────────────┐  │
                    │  │  Internet → WAF → API Gateway    │  │
                    │  │  TLS 1.3 termination             │  │
                    │  │  JWT validation (OIDC)           │  │
                    │  │  Rate limiting (per tenant)      │  │
                    │  │  DDoS protection                 │  │
                    │  └──────────────────────────────────┘  │
                    └──────────────────┬─────────────────────┘
                                       │ Only authenticated
                                       │ requests pass
                    EAST-WEST (Internal Service Traffic)
                    ┌──────────────────▼─────────────────────┐
                    │       SERVICE MESH LAYER               │
                    │  ┌──────────────────────────────────┐  │
                    │  │  Dapr Sentry (mTLS CA)           │  │
                    │  │  Service-to-service mTLS         │  │
                    │  │  Identity: SPIFFE/SPIRE          │  │
                    │  │  Default: deny-all NetworkPolicy  │  │
                    │  │  Allow: explicit per-service ACL  │  │
                    │  └──────────────────────────────────┘  │
                    └──────────────────┬─────────────────────┘
                                       │ mTLS + service identity
                    ┌──────────────────▼─────────────────────┐
                    │            CORE LAYER                  │
                    │  ┌──────────────────────────────────┐  │
                    │  │  Databases: TLS-only connections  │  │
                    │  │  Kafka: SASL/SCRAM + TLS          │  │
                    │  │  Redis: TLS + auth                │  │
                    │  │  Secrets: Dapr secrets (no env)   │  │
                    │  └──────────────────────────────────┘  │
                    └────────────────────────────────────────┘
                    PCI SEGMENT (Isolated)
                    ┌────────────────────────────────────────┐
                    │  ┌──────────────────────────────────┐  │
                    │  │  Tokenization API only           │  │
                    │  │  mTLS client cert required       │  │
                    │  │  Accessible from: payments-svc   │  │
                    │  │  Blocked from: ALL other services │  │
                    │  └──────────────────────────────────┘  │
                    └────────────────────────────────────────┘
```

## Identity Propagation Chain

```
User/Terminal                                                      Database
     │                                                                │
     │  JWT (signed by OIDC provider)                                │
     │  Claims: sub, tenant_id, role, store_id                       │
     ▼                                                               │
API Gateway                                                          │
     │  Validates JWT signature                                       │
     │  Extracts tenant_id → X-Tenant-Id header                     │
     │  Adds X-Correlation-Id                                        │
     │  Forwards JWT in Authorization header                         │
     ▼                                                               │
Service A (e.g., Sales)                                             │
     │  TenantMiddleware extracts ITenantContext                     │
     │  JWT validated again (defense in depth)                       │
     │  When calling Service B: forwards JWT + correlation headers   │
     │  Dapr sidecar adds mTLS client cert (SPIFFE ID)              │
     ▼                                                               │
Service B (e.g., Inventory)                                         │
     │  Verifies mTLS (Dapr Sentry)                                 │
     │  Validates JWT claims                                         │
     │  Sets DB connection schema = tenant_{id}                     │
     ▼                                                               │
PostgreSQL: search_path = tenant_{id} ──────────────────────────────┘
```

## Network Policies (Kubernetes)

```yaml
# Default: deny all ingress and egress
kind: NetworkPolicy
spec:
  podSelector: {}       # applies to all pods
  policyTypes: [Ingress, Egress]
  # No rules = deny all

# Explicit allowances:

# 1. Sales → Dapr sidecar (3500 http, 50001 grpc)
# 2. Dapr sidecar → Kafka (9093 TLS)
# 3. Sales → PostgreSQL (5432)
# 4. Payments → Tokenization (8443 mTLS)  ← only service allowed
# 5. All services → OTel Collector (4317 grpc)
# 6. All services → Dapr Sentry (443)
```

## mTLS Configuration (Dapr)

```yaml
# Dapr Sentry acts as the internal CA
# Every Dapr sidecar gets a SPIFFE X.509 cert:
# SPIFFE ID: spiffe://retail-pos/ns/retail-pos/sales-service

# Certificate rotation: automatic (Dapr handles this)
# Trust domain: retail-pos (configured in dapr-tracing.yaml)

apiVersion: dapr.io/v1alpha1
kind: Configuration
spec:
  mtls:
    enabled: true
    workloadCertTTL: "24h"
    allowedClockSkew: "15m"
```

## PCI Network Segmentation

```
┌──────────────────────────────────────────────────────┐
│  retail-pos namespace                                │
│  ┌────────┐  ┌────────┐  ┌────────┐  ┌───────────┐  │
│  │ sales  │  │ orders │  │inventory│  │ payments  │  │
│  └────────┘  └────────┘  └────────┘  └─────┬─────┘  │
│                                             │ mTLS   │
└─────────────────────────────────────────────┼────────┘
                                              │
┌─────────────────────────────────────────────┼────────┐
│  pci namespace  [ISOLATED]                  │        │
│                                     ┌───────▼──────┐ │
│                                     │ Tokenization │ │
│                                     │    API       │ │
│                                     └──────────────┘ │
│  NetworkPolicy: only payments-service may ingress   │
│  NetworkPolicy: egress only to HSM endpoint          │
└──────────────────────────────────────────────────────┘
```

## Zero-Trust Checklist

| Control | Implementation | Status |
|---|---|---|
| No implicit trust | Default-deny NetworkPolicy | ✅ |
| All traffic encrypted | mTLS (Dapr Sentry) + TLS 1.3 at edge | ✅ |
| Identity-based auth | SPIFFE/SPIRE via Dapr Sentry | ✅ |
| Least privilege | Per-service NetworkPolicy + K8s RBAC | ✅ |
| Secrets not in env | Dapr secrets store (K8s/Vault) | ✅ |
| PCI isolation | Separate namespace + NetworkPolicy | ✅ |
| Audit logging | All requests logged with tenant + identity | ✅ |
| Certificate rotation | Dapr automatic cert rotation | ✅ |
| Non-root pods | `runAsNonRoot: true` in all deployments | ✅ |
| Read-only filesystem | `readOnlyRootFilesystem: true` | ✅ |
