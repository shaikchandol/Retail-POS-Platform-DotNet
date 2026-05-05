# Deployment Topology

## Environment Promotion Strategy

```
┌────────────┐    PR merge     ┌────────────┐   Approval   ┌────────────┐
│    DEV     │ ─────────────► │  STAGING   │ ──────────► │    PROD    │
│            │                │            │              │            │
│ Ephemeral  │                │ Long-lived │              │ Long-lived │
│ Single AZ  │                │ Multi-AZ   │              │ Multi-AZ   │
│ 1 replica  │                │ 2 replicas │              │ 3+ replicas│
│ Local Kafka│                │ Kafka (3)  │              │ Kafka (3)  │
└────────────┘                └────────────┘              └────────────┘

Feature flags:
  - LaunchDarkly / Flagsmith (cloud-agnostic)
  - Feature toggles per tenant
  - Gradual rollout: 1% → 10% → 50% → 100%
```

## Kubernetes Cluster Layout

```
PRODUCTION CLUSTER (CNCF-compliant)
├── Node Pool: system      (3x D4s — control plane workloads)
├── Node Pool: application (3-30x D8s — microservices, HPA-managed)
└── Node Pool: kafka       (3x D16s dedicated — Kafka brokers)

Namespaces:
  retail-pos          ← all microservices + Dapr sidecars
  kafka               ← Strimzi Kafka cluster
  dapr-system         ← Dapr control plane
  observability       ← OTel, Jaeger, Prometheus, Grafana, Loki
  cert-manager        ← TLS certificate automation
  ingress-nginx       ← Ingress controller

Zero Trust Network:
  Default deny-all NetworkPolicy in retail-pos namespace
  Allow only: intra-namespace + Kafka port + external HTTPS (443)
  mTLS between all Dapr sidecars (Dapr Sentry CA)
  Pod security: non-root, read-only filesystem, drop ALL capabilities
```

## Multi-Region Topology (Enterprise Scale)

```
Region A (Primary — e.g., East US / eu-west-1)
┌──────────────────────────────────────────────────┐
│  Full platform stack                             │
│  Kafka cluster (primary)                        │
│  PostgreSQL (primary)                           │
│  Read/Write traffic                             │
└──────────────────────────────────────────────────┘
         │ Kafka MirrorMaker 2 replication
         │ PostgreSQL streaming replication
         ▼
Region B (Secondary — e.g., West US / eu-central-1)
┌──────────────────────────────────────────────────┐
│  Full platform stack (standby)                  │
│  Kafka cluster (replica)                        │
│  PostgreSQL (read replica)                      │
│  Read traffic only + failover target            │
└──────────────────────────────────────────────────┘

RTO: < 15 minutes (automated failover via health checks)
RPO: < 30 seconds (Kafka replication lag + PG streaming)
```

## Per-Service Resource Envelope

| Service | Min Replicas | Max Replicas | CPU Request | Mem Request | Scale Trigger |
|---|---|---|---|---|---|
| Sales | 3 | 20 | 100m | 128Mi | CPU 70% |
| Orders | 2 | 15 | 100m | 128Mi | CPU 70% |
| Payments | 3 | 20 | 100m | 128Mi | CPU 60% |
| Inventory | 2 | 10 | 100m | 128Mi | CPU 70% |
| Pricing | 2 | 8 | 50m | 64Mi | CPU 70% |
| AI Insights | 1 | 5 | 500m | 1Gi | CPU 60% |

## Dapr Sidecar Overhead (Per Pod)

```
CPU: 100m request / 300m limit
Memory: 64Mi request / 256Mi limit
Adds: ~50ms cold start, <1ms warm pub/sub latency
Benefit: retry, circuit break, observability, secret management
```
