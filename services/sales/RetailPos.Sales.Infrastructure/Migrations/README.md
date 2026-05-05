# Sales Service Migrations

## Tenant-Aware Migration Strategy

Migrations are applied per tenant schema. The migration runner:
1. Lists all active tenant schemas from the `platform_tenants` master table
2. For each tenant, sets `search_path = tenant_{id}`
3. Applies pending EF Core migrations in order

```bash
# Apply to all tenants
dotnet ef migrations run --connection "$MASTER_CONNECTION_STRING"

# Apply to a specific tenant (for staged rollouts)
dotnet ef migrations run --tenant acme
```

## Migration Files

Migrations live here and are created via:
```bash
dotnet ef migrations add {MigrationName} \
  --project RetailPos.Sales.Infrastructure \
  --startup-project RetailPos.Sales.Api \
  --output-dir Migrations
```

## Initial Schema

```sql
-- Run once per new tenant onboarding:
CREATE SCHEMA IF NOT EXISTS tenant_{id};
SET search_path = tenant_{id};

-- Then apply all EF migrations
-- event_store table + sale_read_models table
```
