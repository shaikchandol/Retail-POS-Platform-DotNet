# Integration Testing Strategy — Testcontainers + Podman

## Philosophy

> Test against real infrastructure, not mocks.

All integration tests spin up real containers (PostgreSQL, Redis, Kafka, Dapr)
using **Testcontainers for .NET**. No in-memory fakes in the integration test suite.

## Podman (Rootless Container) Support

```bash
# Point Testcontainers at the Podman socket (rootless)
export DOCKER_HOST=unix:///run/user/1000/podman/podman.sock
export TESTCONTAINERS_RYUK_DISABLED=true  # Ryuk does not support rootless
dotnet test --filter "Category=Integration"
```

## Test Layers

```
┌────────────────────────────────────────────────────────────────────┐
│  Unit Tests (no I/O)                                               │
│  • Aggregate logic (SalesTransactionTests)                         │
│  • Value object behaviour (MoneyTests)                             │
│  • Policy engine (GatewayPolicyTests)                              │
│  • No containers needed — fast (<1s per test)                      │
└────────────────────────────────────────────────────────────────────┘
┌────────────────────────────────────────────────────────────────────┐
│  Integration Tests (real containers via Testcontainers)            │
│  • Real PostgreSQL: event store + read model                       │
│  • Real Redis: state store + rate limiter                          │
│  • Real Kafka: event publishing + consumption                      │
│  • Real ASP.NET Core: WebApplicationFactory end-to-end             │
│  • Containers: start per class fixture, share within collection    │
└────────────────────────────────────────────────────────────────────┘
┌────────────────────────────────────────────────────────────────────┐
│  Contract Tests (Pact / consumer-driven contracts)                 │
│  • BFF → downstream service contracts                             │
│  • Event schema contracts (event-contracts/v1)                     │
│  • Gateway policy conformance                                      │
└────────────────────────────────────────────────────────────────────┘
┌────────────────────────────────────────────────────────────────────┐
│  Performance Tests (k6 / NBomber)                                  │
│  • Checkout throughput: target 1,000 TPS per tenant                │
│  • Read model query: p99 < 20ms                                    │
│  • Event store write: p99 < 50ms                                   │
└────────────────────────────────────────────────────────────────────┘
```

## Azure DevOps Pipeline Integration

```yaml
# In pipeline YAML — containers injected as services
jobs:
- job: IntegrationTests
  services:
    postgres:
      image: postgres:16-alpine
      ports: ['5432:5432']
      env: { POSTGRES_DB: test, POSTGRES_PASSWORD: test }
    redis:
      image: redis:7-alpine
      ports: ['6379:6379']
    kafka:
      image: confluentinc/cp-kafka:7.6.0
      ports: ['9092:9092']

  # OR: let Testcontainers manage lifecycle (preferred with Podman)
  # No services needed — tests start/stop containers themselves
```

## Test Patterns

### Shared Container Fixture (Performance)
```csharp
[CollectionDefinition("Integration")]
public class IntegrationCollection : ICollectionFixture<IntegrationFixture> { }

public class IntegrationFixture : IAsyncLifetime
{
    public PostgreSqlContainer Postgres { get; } = new PostgreSqlBuilder()...Build();
    // Start once, share across all tests in the collection
}
```

### WebApplicationFactory + Testcontainers
```csharp
await using var factory = new WebApplicationFactory<Program>()
    .WithWebHostBuilder(host =>
    {
        host.UseSetting("ConnectionStrings:EventStore", fixture.Postgres.GetConnectionString());
    });
```

### Dapr Testcontainers (advanced)
```csharp
// Spin up Dapr sidecar container alongside the app under test
var daprContainer = new DaprContainerBuilder()
    .WithAppId("sales-service")
    .WithComponentsPath("infrastructure/dapr/components/test")
    .Build();
```
