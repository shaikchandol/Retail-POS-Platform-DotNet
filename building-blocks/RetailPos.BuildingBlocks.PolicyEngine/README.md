# Policy Engine Building Block

## Overview

The Policy Engine building block provides a declarative, YAML-driven policy
evaluation framework used by the API Gateway.

## Key Abstractions

```
IFeatureFlagProvider    — flag resolution strategy (in-memory / Redis / remote)
IGatewayPolicyProvider  — policy loader (YAML files / external config)
IPolicyEvaluator        — request evaluation engine
PolicyResult            — Allow / Deny with reason + HTTP status
```

## Swap Points

| Default | Alternative | Swap mechanism |
|---|---|---|
| YAML file loader | OPA (Open Policy Agent) | Implement `IGatewayPolicyProvider` pointing at OPA REST API |
| In-process evaluation | OPA sidecar | Delegate `IPolicyEvaluator.Evaluate()` to OPA via HTTP |
| Static policies | Git-backed dynamic policies | Reload `IGatewayPolicyProvider` on file-system change event |

## Usage in Gateway

```csharp
// In Program.cs
builder.Services.AddSingleton<IGatewayPolicyProvider, YamlGatewayPolicyProvider>();
builder.Services.AddSingleton<IPolicyEvaluator, PolicyEvaluator>();

// In middleware
var result = evaluator.Evaluate(evalCtx);
if (!result.Allowed) { ctx.Response.StatusCode = result.StatusCode; return; }
```

## CI Enforcement

Policy tests (`GatewayPolicyTests.cs`) run in the `QualityGate` stage.
A failing policy test BLOCKS the pipeline — policies cannot be merged
without passing all negative-path tests.

## Policy File Structure

```
policies/
├── gateway/
│   ├── auth-policy.yaml          # Authentication + authorization
│   ├── rate-limiting.yaml        # Per-tenant rate limits + tier overrides
│   └── api-versioning.yaml       # Version routing + sunset enforcement
└── tests/
    └── GatewayPolicyTests.cs     # Automated policy validation
```
