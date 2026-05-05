# Retail POS Platform

Enterprise-scale, cloud-agnostic Retail Point-of-Sale platform built on **.NET 10**, **Apache Kafka**, **Dapr**, and **Kubernetes**.

## Architecture At a Glance

```
POS Terminals → API Gateway → Microservices (CQRS + Event Sourcing)
                                     │
                              Apache Kafka (Dapr abstraction)
                                     │
                   ┌─────────────────┴────────────────────┐
                   ▼                 ▼                     ▼
             Orders Service  Inventory Service    Payments Service
                   ▼                 ▼                     ▼
              Event Store     Read Model (PG)      AI Insights
              (PostgreSQL)    Projections          (Forecasting,
                                                  Fraud, Reco)
```

## Microservices

| Service | Responsibility | Pattern |
|---|---|---|
| **Sales** | Checkout, sale CRUD | Clean Arch + CQRS + Event Sourcing |
| **Orders** | Order lifecycle | Clean Arch + CQRS + Event Sourcing |
| **Payments** | Payment authorisation | Clean Arch + CQRS + Event Sourcing |
| **Inventory** | Stock tracking | Clean Arch + CQRS + Event Sourcing |
| **Pricing** | Price & promotion rules | Rules engine + CQRS |
| **Store Management** | Store/terminal config | CRUD + event publishing |
| **AI Insights** | Forecasting, fraud, personalisation | ML.NET + event consumers |

## Technology Stack

| Layer | Technology |
|---|---|
| Runtime | .NET 10 / C# 13 |
| Containerisation | Podman (rootless), OCI images |
| Orchestration | Kubernetes (CNCF-compliant) |
| Service mesh abstraction | Dapr 1.14 |
| Event backbone | Apache Kafka 3.7 (Strimzi) |
| Databases | PostgreSQL 16 (schema-per-tenant) |
| Cache | Redis 7 (Dapr state store) |
| Secrets | Dapr secret store (K8s / Vault) |
| Observability | OpenTelemetry → Jaeger + Prometheus + Grafana + Loki |
| CI/CD | Azure DevOps Pipelines |
| IaC | Helm charts |

## Mono-Repo Structure

```
retail-pos-platform/
├── services/                   # Microservice implementations
│   ├── sales/                  # Clean Architecture + Vertical Slices
│   ├── orders/
│   ├── payments/
│   ├── inventory/
│   ├── pricing/
│   └── store-management/
├── building-blocks/            # Shared abstractions ONLY
│   ├── Domain/                 # IAggregateRoot, ValueObject, IDomainEvent
│   ├── EventSourcing/          # EventSourcedAggregate, IEventStore
│   ├── Application/            # ICommand, IQuery, Result<T>
│   ├── MultiTenancy/           # ITenantContext, TenantMiddleware
│   └── Dapr/                   # IDaprEventPublisher, DaprEventPublisher
├── event-contracts/            # Versioned, immutable integration events
│   └── v1/                     # sales/, orders/, inventory/, payments/
├── infrastructure/             # Kubernetes, Dapr, Kafka, Helm IaC
│   ├── k8s/                    # Deployment manifests per service
│   ├── dapr/components/        # pubsub, statestore, secretstore, resiliency
│   └── kafka/                  # Strimzi KafkaTopic definitions
├── pipelines/                  # Azure DevOps YAML pipelines
│   ├── sales-service-pipeline.yml
│   ├── infrastructure-pipeline.yml
│   └── templates/              # Reusable pipeline steps
├── ai/                         # AI workloads
│   └── RetailPos.AI.Insights/  # Forecasting, Fraud, Personalisation
└── docs/                       # Architecture docs + ADRs
    ├── architecture/           # HLD, LLD, EventFlows, CleanArch, Deployment
    └── adr/                    # Architecture Decision Records
```

## Quick Start (Local Development)

```bash
# Prerequisites: .NET 10 SDK, Docker/Podman, Dapr CLI, kubectl

# 1. Install Dapr
dapr init

# 2. Start local infrastructure
docker compose -f infrastructure/docker-compose.dev.yml up -d
# Starts: PostgreSQL, Redis, Kafka (single-broker), Kafka UI

# 3. Run Sales Service with Dapr sidecar
dapr run \
  --app-id sales-service \
  --app-port 5001 \
  --dapr-http-port 3500 \
  --components-path infrastructure/dapr/components \
  -- dotnet run --project services/sales/RetailPos.Sales.Api

# 4. Run tests
dotnet test services/sales/ --filter "Category!=Integration"

# 5. Integration tests (requires Docker)
dotnet test services/sales/ --filter "Category=Integration"
```

## Documentation

| Document | Description |
|---|---|
| [HLD.md](docs/architecture/HLD.md) | System context, architecture overview, event flows, multi-tenancy, observability |
| [LLD.md](docs/architecture/LLD.md) | Sales service internals, event sourcing flow, database schema, failure modes |
| [EventFlows.md](docs/architecture/EventFlows.md) | Event-driven workflow diagrams, saga patterns, schema evolution |
| [CleanArchitecture.md](docs/architecture/CleanArchitecture.md) | Layer diagram, dependency rules, vertical slices |
| [DeploymentTopology.md](docs/architecture/DeploymentTopology.md) | Environment promotion, cluster layout, multi-region |
| [MultiTenancyStrategy.md](building-blocks/RetailPos.BuildingBlocks.MultiTenancy/MultiTenancyStrategy.md) | Tenant isolation strategies and trade-offs |
| [SCHEMA_EVOLUTION.md](event-contracts/SCHEMA_EVOLUTION.md) | Event versioning policy and migration patterns |
| [ADR-001](docs/adr/ADR-001-CQRS-EventSourcing.md) | Why CQRS + Event Sourcing |
| [ADR-002](docs/adr/ADR-002-Kafka-Dapr.md) | Why Kafka + Dapr abstraction |
| [ADR-003](docs/adr/ADR-003-MultiTenancy.md) | Schema-per-tenant decision |
| [ADR-004](docs/adr/ADR-004-Podman-Rootless.md) | Why Podman over Docker |

## Design Patterns Applied

| # | Pattern | Location |
|---|---|---|
| 1 | **Microservices** | Entire platform |
| 2 | **Clean Architecture** | Every service |
| 3 | **Vertical Slice Architecture** | `Features/` per service |
| 4 | **CQRS** | Command/Query separation |
| 5 | **Event Sourcing** | All business aggregates |
| 6 | **Mediator** | MediatR for all handlers |
| 7 | **Repository** | Domain↔Infrastructure boundary |
| 8 | **Publisher-Subscriber** | Kafka via Dapr |
| 9 | **Saga (Choreography)** | Cross-service checkout flow |
| 10 | **Circuit Breaker** | Dapr resiliency policies |
| 11 | **Retry + Dead Letter** | Dapr + Kafka DLQ |
| 12 | **Sidecar** | Dapr per-pod |
| 13 | **Schema-per-Tenant** | Multi-tenancy |
| 14 | **Snapshot** | Event store for high-volume streams |
| 15 | **Blue-Green Deployment** | Production pipelines |
| 16 | **Factory Method** | Aggregate creation |
| 17 | **Value Object** | Money, CartItem |
| 18 | **Pipeline Behavior** | MediatR cross-cutting concerns |
| 19 | **Result Pattern** | No exception-based control flow |
| 20 | **Idempotent Consumer** | All Kafka consumers |
