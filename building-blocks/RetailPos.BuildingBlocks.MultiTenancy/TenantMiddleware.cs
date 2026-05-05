using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace RetailPos.BuildingBlocks.MultiTenancy;

/// <summary>
/// Resolves tenant context from HTTP headers or JWT claims.
/// Must run before any authenticated endpoint.
/// </summary>
public class TenantMiddleware(RequestDelegate next, ILogger<TenantMiddleware> logger)
{
    private const string TenantHeader = "X-Tenant-Id";
    private const string StoreHeader = "X-Store-Id";
    private const string TerminalHeader = "X-Terminal-Id";
    private const string TenantClaim = "tenant_id";

    public async Task InvokeAsync(HttpContext ctx, ITenantContext tenantCtx)
    {
        var tc = (TenantContext)tenantCtx;

        // 1. Try header first
        var tenantId = ctx.Request.Headers[TenantHeader].FirstOrDefault();

        // 2. Fall back to JWT claim
        if (string.IsNullOrWhiteSpace(tenantId))
            tenantId = ctx.User.FindFirst(TenantClaim)?.Value;

        if (string.IsNullOrWhiteSpace(tenantId))
        {
            logger.LogWarning("Request received without tenant context: {Path}", ctx.Request.Path);
            ctx.Response.StatusCode = 400;
            await ctx.Response.WriteAsJsonAsync(new { error = "MISSING_TENANT", message = "X-Tenant-Id header or tenant_id claim is required." });
            return;
        }

        var storeId = ctx.Request.Headers[StoreHeader].FirstOrDefault() ?? string.Empty;
        var terminalId = ctx.Request.Headers[TerminalHeader].FirstOrDefault() ?? string.Empty;

        tc.Resolve(tenantId, storeId, terminalId);
        logger.LogDebug("Tenant resolved: {TenantId} / {StoreId} / {TerminalId}", tenantId, storeId, terminalId);

        await next(ctx);
    }
}
