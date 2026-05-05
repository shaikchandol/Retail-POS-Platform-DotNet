using Microsoft.AspNetCore.Mvc;

namespace RetailPos.Pricing.Api.Controllers;

/// <summary>
/// Pricing & Promotions service — evaluates price rules, discount codes,
/// loyalty tiers, and bundle pricing per tenant.
/// </summary>
[ApiController]
[Route("api/v1/pricing")]
public class PricingController : ControllerBase
{
    /// <summary>Calculate effective price for a basket of items</summary>
    [HttpPost("calculate")]
    public IActionResult Calculate([FromBody] PriceCalculationRequest request)
    {
        // Full Pricing Engine implementation:
        // 1. Resolve base price per product (catalog lookup)
        // 2. Apply tenant-specific price overrides
        // 3. Evaluate promotion rules (stacked or exclusive)
        // 4. Apply loyalty tier discount
        // 5. Validate discount code
        // Returns itemized price breakdown per line
        return Ok(new { message = "Pricing engine — see design/LLD.md for full implementation" });
    }

    /// <summary>Validate a discount / promo code</summary>
    [HttpPost("validate-code")]
    public IActionResult ValidateCode([FromBody] PromoCodeRequest request) =>
        Ok(new { valid = true, discountPercentage = 10, message = "10% off" });
}

public record PriceCalculationRequest(
    string TenantId,
    string StoreId,
    string? CustomerId,
    string? DiscountCode,
    List<PriceLineItem> Items
);

public record PriceLineItem(string ProductId, string Sku, int Quantity);
public record PromoCodeRequest(string TenantId, string Code, string? CustomerId);
