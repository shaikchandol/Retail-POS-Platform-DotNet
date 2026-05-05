# Low-Level Design — Sales Service

## 1. Clean Architecture Layers

```
┌─────────────────────────────────────────────────────────────────────┐
│                        API LAYER                                    │
│  ASP.NET Core 10 minimal API / controllers                          │
│  SalesController — thin; delegates to MediatR                       │
│  TenantMiddleware — resolves ITenantContext                         │
│  ExceptionMiddleware — maps domain exceptions to HTTP               │
│  OpenAPI/Swagger — auto-documented endpoints                        │
│                         │ depends on ↓                              │
├─────────────────────────────────────────────────────────────────────┤
│                    APPLICATION LAYER                                │
│  CQRS: Commands + Queries + Handlers                                │
│  Vertical Slice: each feature is a self-contained folder            │
│  MediatR pipeline: Logging → TenantValidation → Validation → Handler│
│  Result<T> pattern — no exception-based control flow                │
│  ISaleReadModelRepository — read model interface                    │
│  IDaprEventPublisher — event publishing interface                   │
│                         │ depends on ↓                              │
├─────────────────────────────────────────────────────────────────────┤
│                      DOMAIN LAYER                                   │
│  SalesTransaction aggregate (event-sourced)                         │
│  CartItem, Money value objects (immutable)                          │
│  SaleDomainEvent hierarchy                                          │
│  No framework dependencies — pure C# / .NET                         │
│  Business invariants enforced in aggregate methods                  │
│                         │ depends on ↓                              │
├─────────────────────────────────────────────────────────────────────┤
│                  INFRASTRUCTURE LAYER                               │
│  PostgresEventStore — event store implementation                    │
│  SaleReadModelProjection — Dapr subscription controller             │
│  SaleReadModelRepository — EF Core read model queries               │
│  DaprEventPublisher — pub/sub via Dapr sidecar                      │
│  EventStoreDbContext — EF Core for event_store table                │
└─────────────────────────────────────────────────────────────────────┘
```

## 2. Vertical Slice Structure (Sales Service)

```
RetailPos.Sales.Application/
  Features/
    CreateSale/                   ← Complete feature slice
      CreateSaleCommand.cs        ← Command DTO
      CreateSaleHandler.cs        ← Business orchestration
      CreateSaleValidator.cs      ← Input validation (FluentValidation)
    CompleteSale/                 ← (if sale is two-phase)
    VoidSale/
      VoidSaleCommand.cs
      VoidSaleHandler.cs
    GetSale/
      GetSaleQuery.cs
      GetSaleHandler.cs           ← Reads from Read Model only
    GetSalesHistory/
      GetSalesHistoryQuery.cs
      GetSalesHistoryHandler.cs
  Projections/
    SaleReadModel.cs              ← Read model DTO + repository interface
  Behaviors/
    TenantValidationBehavior.cs   ← MediatR pipeline
    LoggingBehavior.cs
    ValidationBehavior.cs
```

## 3. Event Sourcing Flow

```
Command → Handler
  │
  ├─ Load aggregate from Event Store (replay events)
  │    EventStore.GetEvents("sale-{id}")
  │    → SalesTransaction.LoadFromHistory(events)
  │    → Aggregate state rebuilt via When() methods
  │
  ├─ Execute command
  │    sale.AddItem(...)  →  RaiseEvent(SaleItemAddedEvent)
  │    sale.Complete(...)  →  RaiseEvent(SaleCompletedEvent)
  │
  ├─ Persist uncommitted events (optimistic concurrency)
  │    EventStore.SaveEvents("sale-{id}", sale.DomainEvents, expectedVersion)
  │    → PostgreSQL INSERT (stream_id, version, event_type, payload)
  │    → ConcurrencyException if version mismatch
  │
  ├─ Publish events via Dapr
  │    DaprEventPublisher.PublishAsync(event)
  │    → Dapr sidecar → Kafka
  │    → Topic: retail.sales.events
  │    → Partition key: tenant-id (guaranteed ordering per tenant)
  │
  └─ Return Result<T>

Projection (Read Model — eventually consistent):
  Kafka → Dapr → SaleProjectionController.OnSaleCompleted(event)
    → repo.UpsertAsync(SaleReadModel)  ← PostgreSQL read table
    → Idempotent: safe to replay
```

## 4. CQRS Read/Write Separation

```
WRITE SIDE (Commands):
  ┌─────────────────────────────────────────────────────┐
  │  Command → Handler → Event Store (PostgreSQL)       │
  │  State: rebuilt by replaying domain events           │
  │  Consistency: strong (within aggregate boundary)    │
  │  Data model: append-only event log                  │
  └─────────────────────────────────────────────────────┘

READ SIDE (Queries):
  ┌─────────────────────────────────────────────────────┐
  │  Query → Handler → Read Model (PostgreSQL view table)│
  │  State: denormalized, query-optimized                │
  │  Consistency: eventual (updated via Kafka events)    │
  │  Data model: flat rows, indexed for query patterns   │
  └─────────────────────────────────────────────────────┘

  Projection:
    Event Store → Kafka → Dapr Subscription
      → SaleProjectionController
        → ISaleReadModelRepository.UpsertAsync()
```

## 5. Idempotency Strategy

```
1. EventId (UUID) as idempotency key for event publishing
2. Event Store: version check prevents duplicate writes
3. Read Model projection: UPSERT by SaleId — safe to replay
4. Kafka consumer: track processed EventIds in Redis
   key: "idempotency:{consumerId}:{eventId}" TTL: 24h
5. Dapr handles at-least-once delivery — consumers handle duplicates
```

## 6. Failure Modes & Handling

```
SCENARIO 1: Event Store write succeeds, Kafka publish fails
  → Dapr retries with exponential backoff (5 retries, max 60s)
  → If still failing → dead letter queue
  → Operator alert: consumer lag > threshold

SCENARIO 2: Projection consumer crashes mid-update
  → Kafka offset not committed → message replayed on restart
  → UPSERT in projection ensures idempotency

SCENARIO 3: Optimistic concurrency conflict
  → ConcurrencyException thrown
  → API returns 409 Conflict
  → Client retries with fresh read

SCENARIO 4: Network partition (Kafka unavailable)
  → Dapr circuit breaker opens after 5 failures
  → Commands that require event publish are rejected (sale still written)
  → Outbox pattern: periodic background job re-publishes uncommitted events
```

## 7. Aggregate Snapshot Strategy

```
Problem: Deep event streams (10,000+ events) take too long to replay.

Solution: Snapshots every N events (default: 500)
  EventStore.GetSnapshot("sale-{id}") → (snapshot, version)
  If snapshot found:
    Reconstitute from snapshot
    Load events FROM snapshotVersion
  If no snapshot:
    Load all events from version 0

Snapshot storage: same PostgreSQL, separate table
snapshot_store (stream_id, version, snapshot_json, created_at)

Sales transactions: typically < 50 events → snapshots not needed.
Inventory items: can accumulate 10,000+ deductions → snapshots critical.
```

## 8. Database Schema (Sales Service)

```sql
-- Event Store
CREATE TABLE event_store (
    stream_id       VARCHAR(200)    NOT NULL,
    version         INT             NOT NULL,
    event_type      VARCHAR(100)    NOT NULL,
    schema_version  VARCHAR(20)     NOT NULL DEFAULT '1.0',
    payload         JSONB           NOT NULL,
    metadata        JSONB           NOT NULL,
    event_id        UUID            NOT NULL DEFAULT gen_random_uuid(),
    created_at      TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    PRIMARY KEY (stream_id, version)
);
CREATE INDEX idx_event_store_type ON event_store(event_type, created_at);
CREATE INDEX idx_event_store_event_id ON event_store(event_id);

-- Read Model
CREATE TABLE sale_read_models (
    id              UUID            PRIMARY KEY,
    tenant_id       VARCHAR(100)    NOT NULL,
    store_id        VARCHAR(100),
    terminal_id     VARCHAR(100),
    customer_id     VARCHAR(200),
    status          VARCHAR(50)     NOT NULL,
    currency        CHAR(3)         NOT NULL,
    sub_total       NUMERIC(18,4)   NOT NULL DEFAULT 0,
    tax_total       NUMERIC(18,4)   NOT NULL DEFAULT 0,
    discount_total  NUMERIC(18,4)   NOT NULL DEFAULT 0,
    grand_total     NUMERIC(18,4)   NOT NULL DEFAULT 0,
    payment_method  VARCHAR(50),
    receipt_number  VARCHAR(100),
    items           JSONB,
    created_at      TIMESTAMPTZ     NOT NULL,
    updated_at      TIMESTAMPTZ     NOT NULL,
    version         INT             NOT NULL DEFAULT 0
);
-- Tenant-scoped indexes
CREATE INDEX idx_sale_tenant_store ON sale_read_models(tenant_id, store_id, created_at DESC);
CREATE INDEX idx_sale_receipt ON sale_read_models(receipt_number);
```
