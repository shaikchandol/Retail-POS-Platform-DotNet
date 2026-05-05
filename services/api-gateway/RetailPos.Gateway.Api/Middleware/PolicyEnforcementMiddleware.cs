using RetailPos.Gateway.Api.Policies;

namespace RetailPos.Gateway.Api.Middleware;

/// <summary>
/// Enforces declarative gateway policies loaded from YAML.
/// Every request is evaluated before reaching the reverse proxy.
/// </summary>
public class PolicyEnforcementMiddleware(RequestDelegate next, IPolicyEvaluator evaluator, ILogger<PolicyEnforcementMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext ctx)
    {
        var evalCtx = new PolicyEvaluationContext(
            RoutePath: ctx.Request.Path.Value ?? "/",
            TenantId: ctx.Request.Headers["X-Tenant-Id"].FirstOrDefault(),
            UserRoles: ctx.User.Claims.Where(c => c.Type == "role").Select(c => c.Value).ToList(),
            Claims: ctx.User.Claims.ToDictionary(c => c.Type, c => c.Value),
            ClientIp: ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown");

        var result = evaluator.Evaluate(evalCtx);

        if (!result.Allowed)
        {
            logger.LogWarning("Policy denied request to {Path} for tenant {Tenant}: {Reason}",
                evalCtx.RoutePath, evalCtx.TenantId, result.DenyReason);

            ctx.Response.StatusCode = result.StatusCode;
            await ctx.Response.WriteAsJsonAsync(new
            {
                error = result.DenyReason,
                path = evalCtx.RoutePath
            });
            return;
        }

        await next(ctx);
    }
}

/// <summary>
/// Enforces strict tenant isolation: requests cannot access another tenant's resources.
/// </summary>
public class TenantIsolationMiddleware(RequestDelegate next, ILogger<TenantIsolationMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext ctx)
    {
        var headerTenant = ctx.Request.Headers["X-Tenant-Id"].FirstOrDefault();
        var claimTenant  = ctx.User.FindFirst("tenant_id")?.Value;

        if (!string.IsNullOrWhiteSpace(headerTenant) &&
            !string.IsNullOrWhiteSpace(claimTenant) &&
            headerTenant != claimTenant)
        {
            logger.LogWarning("Tenant isolation violation: header={Header}, claim={Claim}",
                headerTenant, claimTenant);
            ctx.Response.StatusCode = 403;
            await ctx.Response.WriteAsJsonAsync(new { error = "TENANT_ISOLATION_VIOLATION" });
            return;
        }

        await next(ctx);
    }
}

/// <summary>
/// Injects standard correlation and observability headers on every request.
/// </summary>
public class RequestTransformMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext ctx)
    {
        // Ensure correlation ID propagation
        if (!ctx.Request.Headers.ContainsKey("X-Correlation-Id"))
            ctx.Request.Headers["X-Correlation-Id"] = Guid.NewGuid().ToString();

        // Stamp gateway version
        ctx.Response.Headers["X-Gateway-Version"] = "1.0";

        await next(ctx);
    }
}
