# Event Schema Evolution Policy

## Principles

1. **Events are immutable once published** — never edit a deployed event schema.
2. **Additive changes only in same version** — new optional fields are backward compatible.
3. **Breaking changes require new version** — `SaleCreatedV1` → `SaleCreatedV2`.
4. **Consumers must handle unknown fields** — use `JsonExtensionData` or lenient deserializers.
5. **Producers publish both versions during migration** — dual-write period.

## Versioning Convention

```
{domain}.{event-name}.v{N}

retail.sales.events    → topic
sale.created.v1        → event type in message header
sale.created.v2        → breaking change version
```

## Backward-Compatible Changes (Safe in v1)
- Add new optional field with default value
- Add new enum value (consumers must ignore unknown enums)
- Widen numeric types (int → long)

## Breaking Changes (Requires v2)
- Remove or rename a field
- Change field type (string → Guid)
- Change semantic meaning of a field
- Make optional field required

## Migration Pattern: V1 → V2

```
┌──────────────┐     v1 + v2     ┌──────────────────────────┐
│   Producer   │ ─────────────►  │  Kafka Topic              │
│   (updated)  │                 │  retail.sales.events       │
└──────────────┘                 └──────────────────────────┘
                                      │           │
                                    v1 only     v1 + v2
                                      ▼           ▼
                               ┌──────────┐  ┌──────────┐
                               │Legacy    │  │New       │
                               │Consumer  │  │Consumer  │
                               │(unchanged)│  │(handles  │
                               └──────────┘  │ both)    │
                                             └──────────┘

Step 1: Publish V2 alongside V1 (dual-write, 2 weeks)
Step 2: Migrate all consumers to V2
Step 3: Deprecate V1 publication
Step 4: Remove V1 after retention period
```

## Kafka Topic Naming

| Domain    | Topic                        | Partitions | Retention |
|-----------|------------------------------|------------|-----------|
| Sales     | retail.sales.events          | 24         | 30 days   |
| Orders    | retail.orders.events         | 12         | 30 days   |
| Inventory | retail.inventory.events      | 12         | 7 days    |
| Payments  | retail.payments.events       | 24         | 90 days   |
| Pricing   | retail.pricing.events        | 6          | 7 days    |
| AI        | retail.ai.events             | 6          | 7 days    |

## Idempotency Requirements

All consumers MUST be idempotent:
- Use `EventId` (UUID) as idempotency key
- Store processed event IDs in Redis or PostgreSQL
- Return success without re-processing on duplicate delivery
