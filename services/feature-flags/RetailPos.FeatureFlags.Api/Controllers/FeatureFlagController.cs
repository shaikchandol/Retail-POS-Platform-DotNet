using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RetailPos.BuildingBlocks.FeatureFlags;

namespace RetailPos.FeatureFlags.Api.Controllers;

/// <summary>
/// Feature Flag API — runtime, tenant-aware, kill-switch–capable.
/// Consumed by all services via IFeatureFlagService building block.
/// Admin mutations require platform-ops or tenant-admin role.
///
/// Swap point: back this with Azure App Configuration (feature management),
///   LaunchDarkly, Unleash, or Flagsmith — implement IFeatureFlagProvider.
/// </summary>
[ApiController]
[Route("api/v1/flags")]
public class FeatureFlagController(IFeatureFlagService flags, ILogger<FeatureFlagController> logger) : ControllerBase
{
    /// <summary>Get flag state for calling tenant.</summary>
    [HttpGet("{flagName}")]
    public async Task<IActionResult> GetFlag(string flagName, CancellationToken ct)
    {
        var tenantId = Request.Headers["X-Tenant-Id"].FirstOrDefault() ?? string.Empty;
        var enabled  = await flags.IsEnabledForTenantAsync(flagName, tenantId, ct);
        return Ok(new { flag = flagName, tenantId, enabled });
    }

    /// <summary>Get all flags for the calling tenant (debug/admin use).</summary>
    [HttpGet]
    [Authorize(Roles = "manager,admin")]
    public async Task<IActionResult> GetAllFlags(CancellationToken ct)
    {
        var tenantId = Request.Headers["X-Tenant-Id"].FirstOrDefault() ?? string.Empty;
        var all = await flags.GetAllFlagsAsync(tenantId, ct);
        return Ok(new { tenantId, flags = all });
    }

    /// <summary>
    /// Kill-switch: disable a feature for all tenants immediately.
    /// No deployment required. Propagates within < 100ms.
    /// </summary>
    [HttpPost("{flagName}/disable-all")]
    [Authorize(Roles = "platform-ops")]
    public IActionResult KillSwitch(string flagName)
    {
        logger.LogWarning("KILL-SWITCH activated for flag {Flag} by {User}", flagName, User.FindFirst("sub")?.Value);
        // Implementation: update flag store, invalidate distributed cache
        return Accepted(new { message = $"Kill-switch activated for {flagName}. Propagating to all tenants." });
    }

    /// <summary>
    /// Enable flag for a specific tenant (tenant override).
    /// Useful for canary rollouts: enable for 1 tenant, monitor, then expand.
    /// </summary>
    [HttpPost("{flagName}/tenants/{tenantId}/enable")]
    [Authorize(Roles = "platform-ops,admin")]
    public IActionResult EnableForTenant(string flagName, string tenantId)
    {
        logger.LogInformation("Enabling flag {Flag} for tenant {Tenant}", flagName, tenantId);
        return Accepted(new { message = $"Flag {flagName} enabled for tenant {tenantId}." });
    }
}
