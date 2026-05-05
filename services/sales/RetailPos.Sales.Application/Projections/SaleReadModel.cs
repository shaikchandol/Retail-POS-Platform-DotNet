namespace RetailPos.Sales.Application.Projections;

// ── Read Model (denormalized, optimized for queries) ──────────────────────────
public class SaleReadModel
{
    public Guid Id { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public string StoreId { get; set; } = string.Empty;
    public string TerminalId { get; set; } = string.Empty;
    public string CustomerId { get; set; } = string.Empty;
    public string CashierId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Currency { get; set; } = string.Empty;
    public decimal SubTotal { get; set; }
    public decimal TaxTotal { get; set; }
    public decimal DiscountTotal { get; set; }
    public decimal GrandTotal { get; set; }
    public string? PaymentMethod { get; set; }
    public string? ReceiptNumber { get; set; }
    public string? DiscountCode { get; set; }
    public decimal DiscountPercentage { get; set; }
    public List<SaleItemReadModel> Items { get; set; } = [];
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public int Version { get; set; }
}

public class SaleItemReadModel
{
    public string ProductId { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal LineTotal { get; set; }
}

public interface ISaleReadModelRepository
{
    Task<SaleReadModel?> GetByIdAsync(Guid id, string tenantId, CancellationToken ct = default);
    Task<(IEnumerable<SaleReadModel> Items, int Total)> ListAsync(string tenantId, string? storeId, string? terminalId, DateTimeOffset? from, DateTimeOffset? to, int page, int limit, CancellationToken ct = default);
    Task UpsertAsync(SaleReadModel model, CancellationToken ct = default);
}
