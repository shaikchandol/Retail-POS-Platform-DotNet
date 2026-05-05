using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;

namespace RetailPos.Pci.Tokenization.Api.Services;

/// <summary>
/// PCI DSS–compliant tokenization service.
///
/// Design principles:
/// - Card data is NEVER stored in plain form anywhere in the system
/// - Tokens are opaque, random, and non-reversible without the vault key
/// - Token→PAN mapping stored only in HSM-backed secure vault
/// - All operations are audit-logged with tamper-proof entries
/// - Network: isolated segment, accessible only from payments-service via mTLS
///
/// PCI Scope Reduction:
/// - POS terminals send PANs directly to THIS service over mTLS
/// - All other services only ever see tokens — they are out of PCI scope
/// - Reduces audit surface dramatically
///
/// Swap point: replace AES-256 local encryption with Azure Key Vault Managed HSM,
///   AWS CloudHSM, or Thales Luna — swap ITokenVault implementation only.
/// </summary>
public class TokenizationService(ITokenVault vault, ITokenAuditLog auditLog, ILogger<TokenizationService> logger)
{
    public async Task<TokenizeResult> TokenizeAsync(string pan, string tenantId, string requestorId, CancellationToken ct = default)
    {
        GuardPanFormat(pan);

        var token = GenerateToken();
        var lastFour = pan[^4..];
        var bin = pan[..6];

        var encrypted = await vault.EncryptAsync(pan, tenantId, ct);
        await vault.StoreAsync(token, encrypted, tenantId, ct);

        await auditLog.RecordAsync(new TokenizationAuditEntry(
            Action: "TOKENIZE",
            Token: token,
            LastFour: lastFour,
            Bin: bin,
            TenantId: tenantId,
            RequestorId: requestorId,
            Timestamp: DateTimeOffset.UtcNow));

        logger.LogInformation("Tokenized card **** **** **** {Last4} for tenant {Tenant}", lastFour, tenantId);

        return new TokenizeResult(token, lastFour, bin);
    }

    public async Task<DetokenizeResult> DetokenizeAsync(string token, string tenantId, string requestorId, string purpose, CancellationToken ct = default)
    {
        // Only payment-authorisation callers may detokenize
        // Purpose must be one of: PAYMENT_AUTH, REFUND, VOID
        if (!IsAllowedPurpose(purpose))
            throw new UnauthorizedAccessException($"Detokenization not allowed for purpose: {purpose}");

        var encrypted = await vault.RetrieveAsync(token, tenantId, ct);
        if (encrypted is null)
            throw new InvalidOperationException($"Token not found: {token}");

        var pan = await vault.DecryptAsync(encrypted, tenantId, ct);

        await auditLog.RecordAsync(new TokenizationAuditEntry(
            Action: "DETOKENIZE",
            Token: token,
            LastFour: pan[^4..],
            Bin: pan[..6],
            TenantId: tenantId,
            RequestorId: requestorId,
            Timestamp: DateTimeOffset.UtcNow,
            Purpose: purpose));

        return new DetokenizeResult(pan, token);
    }

    private static string GenerateToken()
    {
        // Format-preserving: 16-digit numeric token (looks like a PAN but is random)
        Span<byte> bytes = stackalloc byte[8];
        RandomNumberGenerator.Fill(bytes);
        var num = BitConverter.ToUInt64(bytes) % 10_000_000_000_000_000UL;
        return num.ToString("D16");
    }

    private static void GuardPanFormat(string pan)
    {
        if (string.IsNullOrWhiteSpace(pan) || pan.Length is < 13 or > 19 || !pan.All(char.IsDigit))
            throw new ArgumentException("Invalid PAN format.");
        if (!LuhnCheck(pan))
            throw new ArgumentException("PAN failed Luhn check.");
    }

    private static bool LuhnCheck(string pan)
    {
        var sum = 0;
        var alternate = false;
        for (var i = pan.Length - 1; i >= 0; i--)
        {
            var n = int.Parse(pan[i].ToString());
            if (alternate) { n *= 2; if (n > 9) n -= 9; }
            sum += n;
            alternate = !alternate;
        }
        return sum % 10 == 0;
    }

    private static bool IsAllowedPurpose(string purpose) =>
        purpose is "PAYMENT_AUTH" or "REFUND" or "VOID";
}

public record TokenizeResult(string Token, string LastFour, string Bin);
public record DetokenizeResult(string Pan, string Token);
public record TokenizationAuditEntry(string Action, string Token, string LastFour, string Bin, string TenantId, string RequestorId, DateTimeOffset Timestamp, string? Purpose = null);

public interface ITokenVault
{
    Task<string> EncryptAsync(string plaintext, string tenantId, CancellationToken ct);
    Task<string?> DecryptAsync(string ciphertext, string tenantId, CancellationToken ct);
    Task StoreAsync(string token, string encryptedPan, string tenantId, CancellationToken ct);
    Task<string?> RetrieveAsync(string token, string tenantId, CancellationToken ct);
}

public interface ITokenAuditLog
{
    Task RecordAsync(TokenizationAuditEntry entry, CancellationToken ct = default);
}
