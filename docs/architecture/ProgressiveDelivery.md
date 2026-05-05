# Progressive Delivery

## Deployment Strategies

### 1. Blue-Green Deployment (Production Default)

```
BLUE (current)                GREEN (new version)
─────────────────────         ─────────────────────
sales-service:v1.2            sales-service:v1.3
3 replicas                    3 replicas (warm, pre-deployed)
100% traffic                  0% traffic
                 │
                 │ Switch (YARP route weight: v1.3=100%)
                 ▼
BLUE (old)                    GREEN (now active)
─────────────────────         ─────────────────────
sales-service:v1.2            sales-service:v1.3
Standby (15 min)              3 replicas
Rollback target               100% traffic

Rollback: re-weight to BLUE (< 30 seconds, no downtime)
```

### 2. Canary Release (Risk-Managed Feature Rollout)

```
Stage 1: Feature flag enabled for 1 internal tenant
  ─► Monitor error rate, latency, SLO for 24h

Stage 2: Enable for 10% of tenants (random sample)
  ─► Monitor for 48h
  ─► Auto-rollback if error rate > 0.5%

Stage 3: Enable for 50% of tenants
  ─► Monitor for 24h

Stage 4: Full rollout (100% of tenants)
  ─► Feature flag removed in next release
```

### 3. Tenant-Scoped Rollout

```
New feature: "AI Personalization" (FeatureFlags.AiPersonalization)

Week 1:   Enable for pilot tenants (opt-in)
Week 2:   Enable for enterprise tier
Week 3:   Enable for professional tier
Week 4:   Enable for all (starter tier)

Emergency kill-switch:
  POST /api/v1/admin/feature-flags/ai-personalization/disable-all
  Effect: immediate, no deployment needed, < 100ms propagation
```

---

## Azure DevOps Pipeline — Progressive Stages

```yaml
stages:
- stage: Deploy_Dev
  # Automatic on every main branch push
  jobs:
  - deployment: sales_dev
    environment: retail-pos-dev

- stage: Deploy_Staging
  dependsOn: Deploy_Dev
  # Automatic after Dev passes
  jobs:
  - deployment: sales_staging
    environment: retail-pos-staging
    strategy:
      runOnce:
        deploy: { }

- stage: Deploy_Prod_Canary
  dependsOn: Deploy_Staging
  condition: and(succeeded(), eq(variables['Deploy.Production'], 'true'))
  jobs:
  - deployment: sales_canary
    environment: retail-pos-prod-canary
    strategy:
      canary:
        increments: [10, 25, 50, 100]  # % of traffic over time
        preDeploy:
          steps: [ validatePolicy, runSmokeTests ]
        deploy:
          steps: [ deployManifest ]
        postRouteTraffic:
          steps: [ validateSlo, monitorErrorRate ]
        on:
          failure:
            steps: [ rollback ]

- stage: Deploy_Prod_BlueGreen
  dependsOn: Deploy_Prod_Canary
  jobs:
  - deployment: sales_prod
    environment: retail-pos-prod
    strategy:
      runOnce:
        deploy:
          steps:
          - task: KubernetesManifest@1
            inputs:
              strategy: blueGreen
              action: deploy
```

---

## Feature Flag Integration in Pipeline

```yaml
# Pipeline: check feature flag state before deploying
- script: |
    FLAG=$(curl -s -H "X-Tenant-Id: platform" \
      "${FEATURE_FLAG_URL}/api/v1/flags/checkout-saga-orchestrator")
    echo "Flag state: $FLAG"
    if [ "$FLAG" = "false" ]; then
      echo "Feature flag disabled — skipping saga orchestrator deployment"
      exit 0
    fi
  displayName: Check Feature Flag — Saga Orchestrator
```
