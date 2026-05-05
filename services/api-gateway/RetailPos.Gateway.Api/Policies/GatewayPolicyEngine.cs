using System.Text.Json;
using YamlDotNet.Serialization;

namespace RetailPos.Gateway.Api.Policies;

/// <summary>
/// Policy-as-Code engine. Policies are declared in YAML files (policies/gateway/),
/// version-controlled, peer-reviewed, and loaded at startup.
/// The evaluator applies policies to every inbound request.
///
/// Swap point: replace YAML loader with OPA (Open Policy Agent) by pointing
/// IPolicyEvaluator at an OPA sidecar — no middleware changes required.
/// </summary>
public interface IGatewayPolicyProvider
{
    GatewayPolicySet GetPolicies();
}

public class YamlGatewayPolicyProvider(IWebHostEnvironment env, ILogger<YamlGatewayPolicyProvider> logger)
    : IGatewayPolicyProvider
{
    private GatewayPolicySet? _cached;

    public GatewayPolicySet GetPolicies()
    {
        if (_cached is not null) return _cached;

        var policyDir = Path.Combine(env.ContentRootPath, "Policies");
        var deserializer = new DeserializerBuilder().Build();
        var set = new GatewayPolicySet();

        foreach (var file in Directory.EnumerateFiles(policyDir, "*.yaml"))
        {
            var yaml = File.ReadAllText(file);
            var policy = deserializer.Deserialize<GatewayPolicy>(yaml);
            set.Policies.Add(policy);
            logger.LogInformation("Loaded gateway policy: {Policy}", policy.Name);
        }

        _cached = set;
        return set;
    }
}

public interface IPolicyEvaluator
{
    PolicyResult Evaluate(PolicyEvaluationContext ctx);
}

public class PolicyEvaluator(IGatewayPolicyProvider provider) : IPolicyEvaluator
{
    public PolicyResult Evaluate(PolicyEvaluationContext ctx)
    {
        var policies = provider.GetPolicies().Policies
            .Where(p => p.AppliesTo(ctx.RoutePath))
            .OrderBy(p => p.Priority);

        foreach (var policy in policies)
        {
            var result = policy.Evaluate(ctx);
            if (!result.Allowed)
                return result;
        }

        return PolicyResult.Allow();
    }
}

// ── Policy Model ──────────────────────────────────────────────────────────────
public class GatewayPolicySet { public List<GatewayPolicy> Policies { get; set; } = []; }

public class GatewayPolicy
{
    public string Name { get; set; } = string.Empty;
    public int Priority { get; set; } = 100;
    public string PathPattern { get; set; } = "/**";
    public List<string> AllowedRoles { get; set; } = [];
    public List<string> RequiredClaims { get; set; } = [];
    public RateLimitPolicy? RateLimit { get; set; }
    public bool RequiresTenant { get; set; } = true;
    public List<string> BlockedTenants { get; set; } = [];

    public bool AppliesTo(string path) =>
        System.Text.RegularExpressions.Regex.IsMatch(path,
            PathPattern.Replace("**", ".*").Replace("*", "[^/]*"));

    public PolicyResult Evaluate(PolicyEvaluationContext ctx)
    {
        // Tenant check
        if (RequiresTenant && string.IsNullOrWhiteSpace(ctx.TenantId))
            return PolicyResult.Deny("MISSING_TENANT", 400);

        // Blocked tenant
        if (BlockedTenants.Contains(ctx.TenantId ?? string.Empty))
            return PolicyResult.Deny("TENANT_BLOCKED", 403);

        // Role check
        if (AllowedRoles.Any() && !AllowedRoles.Any(r => ctx.UserRoles.Contains(r)))
            return PolicyResult.Deny("INSUFFICIENT_ROLE", 403);

        // Required claims
        foreach (var claim in RequiredClaims)
            if (!ctx.Claims.ContainsKey(claim))
                return PolicyResult.Deny($"MISSING_CLAIM:{claim}", 403);

        return PolicyResult.Allow();
    }
}

public class RateLimitPolicy { public int RequestsPerMinute { get; set; } = 1000; }

public record PolicyEvaluationContext(
    string RoutePath, string? TenantId, List<string> UserRoles,
    Dictionary<string, string> Claims, string ClientIp);

public record PolicyResult(bool Allowed, string? DenyReason = null, int StatusCode = 200)
{
    public static PolicyResult Allow() => new(true);
    public static PolicyResult Deny(string reason, int status = 403) => new(false, reason, status);
}
