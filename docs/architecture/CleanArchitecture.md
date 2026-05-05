# Clean Architecture — Sales Service Deep Dive

## Layer Diagram (3D Conceptual View)

```
                          ╔══════════════════════════════════╗
                          ║         API LAYER                ║  ← outermost ring
                          ║  Controllers · Middleware        ║
                          ║  Swagger · Auth · Rate Limit     ║
                          ╚════════════════╤═════════════════╝
                                           │ depends on
                          ╔════════════════▼═════════════════╗
                          ║      APPLICATION LAYER           ║
                          ║  Commands · Queries · Handlers   ║
                          ║  Behaviors · Read Models         ║
                          ║  Interfaces (ports)              ║
                          ╚════════════════╤═════════════════╝
                                           │ depends on
                          ╔════════════════▼═════════════════╗
                          ║         DOMAIN LAYER             ║  ← innermost / pure
                          ║  Aggregates · Domain Events      ║
                          ║  Value Objects · Domain Rules    ║
                          ║  No framework dependencies       ║
                          ╚══════════════════════════════════╝

                          ╔══════════════════════════════════╗
                          ║     INFRASTRUCTURE LAYER         ║  ← implements ports
                          ║  EventStore · ReadModel Repo     ║
                          ║  DaprPublisher · EF Core         ║
                          ║  Projection Controllers          ║
                          ╚══════════════════════════════════╝
                                 ▲            ▲
                                 │            │
                          implements     implements
                          IEventStore    ISaleReadModelRepository
                          (defined in Application layer)
```

## Dependency Rules (Non-Negotiable)

```
✅ API → Application → Domain
✅ Infrastructure → Application → Domain
✅ Infrastructure implements interfaces defined in Application
❌ Domain NEVER references Application or Infrastructure
❌ Application NEVER references Infrastructure directly
❌ API NEVER contains business logic
❌ Two microservices NEVER share a database
```

## Key Abstractions (Ports)

```csharp
// Defined in Application — implemented in Infrastructure
public interface IEventStore { ... }
public interface ISaleReadModelRepository { ... }
public interface IDaprEventPublisher { ... }
public interface ITenantContext { ... }  // from BuildingBlocks
```

## Vertical Slice vs Horizontal Slice

```
HORIZONTAL (traditional layered — NOT used):
  Controllers/    → all controllers together
  Handlers/       → all handlers together
  Repositories/   → all repos together
  Problem: adding a feature touches 5+ folders

VERTICAL SLICE (this platform):
  Features/
    CreateSale/   → Command + Handler + Validator (self-contained)
    GetSale/      → Query + Handler (self-contained)
    VoidSale/     → Command + Handler (self-contained)
  Adding a feature = adding ONE folder with 2-3 files
```

## Building Blocks — Shared Abstractions Only

```
building-blocks/
  RetailPos.BuildingBlocks.Domain/
    IAggregateRoot, IDomainEvent, ValueObject
    → Contains ZERO business logic
    → Domain primitives only

  RetailPos.BuildingBlocks.EventSourcing/
    EventSourcedAggregate<T> (base class)
    IEventStore, ISnapshotStore (interfaces)
    EventRecord, EventMetadata (infrastructure DTOs)

  RetailPos.BuildingBlocks.Application/
    ICommand<T>, IQuery<T>, Result<T>
    → CQRS contract types

  RetailPos.BuildingBlocks.MultiTenancy/
    ITenantContext, TenantContext, TenantMiddleware

  RetailPos.BuildingBlocks.Dapr/
    IDaprEventPublisher, DaprEventPublisher

⚠️ Rule: Building blocks contain ABSTRACTIONS, never domain logic.
   Sales business rules live in Sales.Domain — NEVER in BuildingBlocks.
```
