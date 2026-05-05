namespace RetailPos.BuildingBlocks.FeatureFlags;

/// <summary>
/// Tenant-aware, environment-specific feature flag service.
///
/// Swap point: back this with Microsoft.FeatureManagement (Azure App Config),
///   LaunchDarkly, Flagsmith, or Unleash — swap the implementation only.
///   The interface stays stable; callers are never aware of the provider.
///
/// Features:
/// - Runtime toggles without redeployment
/// - Tenant-level overrides (feature enabled for ACME, disabled for BETA)
/// - Environment-specific (staging can enable features not yet in prod)
/// - Kill-switch: disable features instantly per tenant or globally
/// - Canary: enable for % of tenants (progressive delivery)
/// </summary>
public interface IFeatureFlagService
{
    /// <summary>Is feature enabled globally?</summary>
    Task<bool> IsEnabledAsync(string featureName, CancellationToken ct = default);

    /// <summary>Is feature enabled for this specific tenant?</summary>
    Task<bool> IsEnabledForTenantAsync(string featureName, string tenantId, CancellationToken ct = default);

    /// <summary>Get all flags and their values for a tenant (for debugging/admin)</summary>
    Task<IReadOnlyDictionary<string, bool>> GetAllFlagsAsync(string tenantId, CancellationToken ct = default);
}

public class FeatureFlagService(IFeatureFlagProvider provider) : IFeatureFlagService
{
    public Task<bool> IsEnabledAsync(string featureName, CancellationToken ct = default) =>
        provider.GetValueAsync(featureName, tenantId: null, ct);

    public Task<bool> IsEnabledForTenantAsync(string featureName, string tenantId, CancellationToken ct = default) =>
        provider.GetValueAsync(featureName, tenantId, ct);

    public Task<IReadOnlyDictionary<string, bool>> GetAllFlagsAsync(string tenantId, CancellationToken ct = default) =>
        provider.GetAllAsync(tenantId, ct);
}

public interface IFeatureFlagProvider
{
    Task<bool> GetValueAsync(string featureName, string? tenantId, CancellationToken ct);
    Task<IReadOnlyDictionary<string, bool>> GetAllAsync(string tenantId, CancellationToken ct);
}

// ── Feature flag definitions (type-safe constants) ────────────────────────────
public static class FeatureFlags
{
    public const string ApiV2Preview            = "api-v2-preview";
    public const string CheckoutSagaOrchestrator= "checkout-saga-orchestrator";
    public const string OfflineMode             = "offline-mode";
    public const string AiPersonalization       = "ai-personalization";
    public const string FraudDetectionRealtime  = "fraud-detection-realtime";
    public const string DynamicPricing          = "dynamic-pricing";
    public const string MultiCurrencyCheckout   = "multi-currency-checkout";
    public const string SelfCheckout            = "self-checkout-kiosk";
}

// ── MediatR behavior: skip feature-flagged handlers ─────────────────────────
[AttributeUsage(AttributeTargets.Class)]
public class RequiresFeatureAttribute(string featureName) : Attribute
{
    public string FeatureName { get; } = featureName;
}
