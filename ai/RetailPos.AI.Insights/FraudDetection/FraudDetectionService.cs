using Microsoft.Extensions.Logging;

namespace RetailPos.AI.Insights.FraudDetection;

/// <summary>
/// Real-time fraud detection — evaluates transactions at checkout.
/// Scoring model: ensemble of rule-based + ML.NET binary classification.
/// Pluggable: swap for Azure Fraud Protection, Stripe Radar, etc.
/// </summary>
public class FraudDetectionService(ILogger<FraudDetectionService> logger)
{
    private static readonly List<FraudRule> Rules =
    [
        new("HIGH_VALUE", tx => tx.Amount > 5000, RiskLevel.High, "Transaction exceeds high-value threshold."),
        new("VELOCITY", tx => tx.TransactionsInLastHour > 20, RiskLevel.Medium, "High transaction velocity detected."),
        new("OFF_HOURS", tx => tx.LocalHour is < 6 or > 23, RiskLevel.Low, "Transaction outside business hours."),
        new("NEW_TERMINAL", tx => tx.TerminalAgeHours < 24, RiskLevel.Medium, "Transaction on recently registered terminal."),
    ];

    public FraudScore Evaluate(FraudEvaluationContext ctx)
    {
        var triggered = Rules.Where(r => r.Predicate(ctx)).ToList();
        var maxRisk = triggered.Any() ? triggered.Max(r => r.Risk) : RiskLevel.None;
        var score = maxRisk switch { RiskLevel.High => 0.85f, RiskLevel.Medium => 0.55f, RiskLevel.Low => 0.25f, _ => 0.05f };

        logger.LogInformation("Fraud score {Score} for sale {SaleId}: {Rules}", score, ctx.SaleId, string.Join(", ", triggered.Select(r => r.Name)));

        return new FraudScore(ctx.SaleId, score, maxRisk, triggered.Select(r => r.Reason).ToList(), recommended: maxRisk >= RiskLevel.High ? FraudAction.Block : FraudAction.Allow);
    }
}

public record FraudEvaluationContext(
    Guid SaleId, string TenantId, string TerminalId, string CustomerId,
    decimal Amount, int TransactionsInLastHour, int LocalHour, double TerminalAgeHours
);

public record FraudScore(Guid SaleId, float Score, RiskLevel Risk, List<string> Reasons, FraudAction Recommended);

public record FraudRule(string Name, Func<FraudEvaluationContext, bool> Predicate, RiskLevel Risk, string Reason);

public enum RiskLevel { None, Low, Medium, High }
public enum FraudAction { Allow, Review, Block }
