namespace RetailPos.BuildingBlocks.MultiTenancy;

/// <summary>
/// Ambient tenant context. Populated by middleware from:
/// - HTTP header: X-Tenant-Id
/// - JWT claim: tenant_id
/// - Event metadata
/// Scoped per-request / per-consumer.
/// </summary>
public interface ITenantContext
{
    string TenantId { get; }
    string StoreId { get; }
    string TerminalId { get; }
    bool IsResolved { get; }
}

public class TenantContext : ITenantContext
{
    public string TenantId { get; private set; } = string.Empty;
    public string StoreId { get; private set; } = string.Empty;
    public string TerminalId { get; private set; } = string.Empty;
    public bool IsResolved { get; private set; }

    public void Resolve(string tenantId, string storeId = "", string terminalId = "")
    {
        TenantId = tenantId;
        StoreId = storeId;
        TerminalId = terminalId;
        IsResolved = true;
    }
}
