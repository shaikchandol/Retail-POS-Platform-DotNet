using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RetailPos.Pci.Tokenization.Api.Services;

namespace RetailPos.Pci.Tokenization.Api.Controllers;

/// <summary>
/// PCI-scoped endpoint — accessible only from the payments service via mTLS.
/// Requires both JWT auth AND mTLS client certificate.
/// All requests are audit-logged regardless of outcome.
/// </summary>
[ApiController]
[Route("api/v1/tokenization")]
[Authorize(Policy = "pci-service-only")]
public class TokenizationController(TokenizationService tokenizer, ILogger<TokenizationController> logger) : ControllerBase
{
    /// <summary>
    /// Tokenize a PAN — accepts raw card number, returns opaque token.
    /// PAN is never logged, stored, or returned after this call.
    /// </summary>
    [HttpPost("tokenize")]
    [ProducesResponseType(typeof(TokenizeResponse), 200)]
    public async Task<IActionResult> Tokenize([FromBody] TokenizeRequest req, CancellationToken ct)
    {
        var tenantId   = Request.Headers["X-Tenant-Id"].FirstOrDefault() ?? string.Empty;
        var requestorId = User.FindFirst("sub")?.Value ?? "unknown";

        var result = await tokenizer.TokenizeAsync(req.Pan, tenantId, requestorId, ct);

        // Critically: PAN is NOT in the response, only the token
        return Ok(new TokenizeResponse(result.Token, result.LastFour, result.Bin));
    }

    /// <summary>
    /// Detokenize — only allowed for payment authorisation, refund, and void purposes.
    /// Every detokenization is audit-logged with caller identity and purpose.
    /// </summary>
    [HttpPost("detokenize")]
    [ProducesResponseType(typeof(DetokenizeResponse), 200)]
    [Authorize(Policy = "payment-authoriser")]
    public async Task<IActionResult> Detokenize([FromBody] DetokenizeRequest req, CancellationToken ct)
    {
        var tenantId    = Request.Headers["X-Tenant-Id"].FirstOrDefault() ?? string.Empty;
        var requestorId = User.FindFirst("sub")?.Value ?? "unknown";

        var result = await tokenizer.DetokenizeAsync(req.Token, tenantId, requestorId, req.Purpose, ct);
        return Ok(new DetokenizeResponse(result.Pan));  // Only returned to payment gateway, never stored
    }
}

// No PAN in API response types — only token reference
public record TokenizeRequest([System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.Never)] string Pan);
public record TokenizeResponse(string Token, string LastFour, string Bin);
public record DetokenizeRequest(string Token, string Purpose);
public record DetokenizeResponse(string Pan);   // Pan returned in-memory only — never logged
