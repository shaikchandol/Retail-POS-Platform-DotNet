using Microsoft.AspNetCore.Mvc;

namespace RetailPos.Bff.Pos.Controllers;

/// <summary>
/// POS BFF — Backend For Frontend for Point-of-Sale terminals.
///
/// Responsibilities:
/// - Aggregate data from Sales, Pricing, Inventory in ONE call (reduces terminal round-trips)
/// - Shape responses for low-bandwidth POS hardware (minimal payloads)
/// - Cache pricing and product catalogue locally (offline resilience)
/// - No domain logic — pure aggregation and shaping
///
/// Design principle: each BFF is owned by the client team, NOT the API team.
/// POS team controls this BFF. Changes here don't require API team approval.
/// </summary>
[ApiController]
[Route("bff/pos/v1")]
public class CheckoutBffController(
    ISalesServiceClient salesClient,
    IPricingServiceClient pricingClient,
    IInventoryServiceClient inventoryClient,
    ILogger<CheckoutBffController> logger) : ControllerBase
{
    /// <summary>
    /// Checkout screen init — returns everything the terminal needs in ONE call:
    /// product catalogue (cached), current promotions, cashier info.
    /// Designed for low-latency terminal boot.
    /// </summary>
    [HttpGet("session/init")]
    public async Task<IActionResult> InitSession(CancellationToken ct)
    {
        var tenantId = Request.Headers["X-Tenant-Id"].FirstOrDefault() ?? string.Empty;
        var storeId  = Request.Headers["X-Store-Id"].FirstOrDefault() ?? string.Empty;

        // Fan-out: parallel requests to downstream services
        var (promos, topProducts) = await Task.WhenAll(
            pricingClient.GetActivePromotionsAsync(tenantId, storeId, ct),
            inventoryClient.GetTopProductsAsync(tenantId, storeId, limit: 200, ct)
        ).ContinueWith(t => (t.Result[0], t.Result[1]));

        return Ok(new PosSessionInitResponse(
            StoreId: storeId,
            ActivePromotions: (List<object>)promos,
            TopProducts: (List<object>)topProducts,
            ServerTimestamp: DateTimeOffset.UtcNow,
            OfflineTtlSeconds: 900    // terminal may operate offline for 15 min
        ));
    }

    /// <summary>
    /// Scan barcode — returns product info + live pricing in one round-trip.
    /// </summary>
    [HttpGet("products/{barcode}")]
    public async Task<IActionResult> ScanBarcode(string barcode, CancellationToken ct)
    {
        var tenantId = Request.Headers["X-Tenant-Id"].FirstOrDefault() ?? string.Empty;
        var storeId  = Request.Headers["X-Store-Id"].FirstOrDefault() ?? string.Empty;

        var (product, price) = await (
            inventoryClient.GetByBarcodeAsync(tenantId, barcode, ct),
            pricingClient.GetLivePriceAsync(tenantId, barcode, storeId, ct)
        ).AwaitBoth();

        if (product is null)
            return NotFound(new { error = "PRODUCT_NOT_FOUND", barcode });

        // POS-optimised payload — only what the terminal screen needs
        return Ok(new
        {
            productId = product.ProductId,
            name = product.Name,
            sku = product.Sku,
            unitPrice = price?.EffectivePrice ?? product.ListPrice,
            taxRate = price?.TaxRate ?? 0.1m,
            promotion = price?.AppliedPromotion,
            stockAvailable = product.QuantityOnHand > 0,
        });
    }

    /// <summary>
    /// Submit checkout — initiates sale + payment in one BFF-aggregated call.
    /// The BFF coordinates the sequence, the terminal only makes one request.
    /// </summary>
    [HttpPost("checkout")]
    public async Task<IActionResult> Checkout([FromBody] PosCheckoutRequest req, CancellationToken ct)
    {
        logger.LogInformation("POS checkout for terminal {Terminal} tenant {Tenant}",
            Request.Headers["X-Terminal-Id"].FirstOrDefault(), Request.Headers["X-Tenant-Id"].FirstOrDefault());

        var saleResult = await salesClient.CreateSaleAsync(req.ToCreateSaleCommand(), ct);
        if (!saleResult.IsSuccess)
            return BadRequest(new { error = saleResult.Error });

        // Return POS-optimised receipt format
        return Ok(new PosReceiptResponse(
            SaleId: saleResult.SaleId,
            ReceiptNumber: saleResult.ReceiptNumber,
            TotalAmount: saleResult.TotalAmount,
            Currency: saleResult.Currency,
            PrintableLines: saleResult.ToPrintableLines()
        ));
    }
}

// POS-shaped DTOs — owned by POS BFF, not the Sales domain
public record PosSessionInitResponse(string StoreId, List<object> ActivePromotions, List<object> TopProducts, DateTimeOffset ServerTimestamp, int OfflineTtlSeconds);
public record PosCheckoutRequest(string CustomerId, string PaymentMethod, List<PosLineItem> Items, string? DiscountCode = null);
public record PosLineItem(string Barcode, string ProductId, int Quantity, decimal UnitPrice);
public record PosReceiptResponse(Guid SaleId, string ReceiptNumber, decimal TotalAmount, string Currency, List<string> PrintableLines);

// Typed HTTP client interfaces (implementations use HttpClient + circuit breaker)
public interface ISalesServiceClient { Task<dynamic> CreateSaleAsync(object cmd, CancellationToken ct); }
public interface IPricingServiceClient
{
    Task<List<object>> GetActivePromotionsAsync(string tenantId, string storeId, CancellationToken ct);
    Task<dynamic?> GetLivePriceAsync(string tenantId, string barcode, string storeId, CancellationToken ct);
}
public interface IInventoryServiceClient
{
    Task<List<object>> GetTopProductsAsync(string tenantId, string storeId, int limit, CancellationToken ct);
    Task<dynamic?> GetByBarcodeAsync(string tenantId, string barcode, CancellationToken ct);
}

// Extension for parallel tuple awaiting
public static class TaskExtensions
{
    public static async Task<(T1, T2)> AwaitBoth<T1, T2>(this (Task<T1> t1, Task<T2> t2) tasks)
    {
        await Task.WhenAll(tasks.t1, tasks.t2);
        return (tasks.t1.Result, tasks.t2.Result);
    }
}
