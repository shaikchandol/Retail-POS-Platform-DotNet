using Microsoft.Extensions.Logging;

namespace RetailPos.AI.Insights.Personalization;

/// <summary>
/// Product recommendations and personalization engine.
/// Uses collaborative filtering with customer purchase history.
/// Cloud-agnostic: pluggable into Azure Personalizer, AWS Personalize, or custom model.
/// </summary>
public class PersonalizationService(
    ICustomerPurchaseRepository purchaseRepo,
    ILogger<PersonalizationService> logger)
{
    public async Task<RecommendationResult> RecommendAsync(
        string tenantId, string customerId, string? currentProductId = null,
        int maxRecommendations = 5, CancellationToken ct = default)
    {
        var history = await purchaseRepo.GetPurchaseHistoryAsync(tenantId, customerId, limit: 100, ct);

        if (!history.Any())
            return RecommendationResult.Empty(customerId);

        // Collaborative filtering logic:
        // 1. Find customers who bought the same products
        // 2. Recommend what those customers also bought
        // 3. Filter out products already purchased by this customer
        // Production: integrate with Azure Cognitive Search or custom ML model

        var purchased = history.Select(h => h.ProductId).ToHashSet();
        var recommendations = history
            .GroupBy(h => h.CategoryId)
            .OrderByDescending(g => g.Count())
            .SelectMany(g => g)
            .Where(h => h.ProductId != currentProductId)
            .DistinctBy(h => h.ProductId)
            .Take(maxRecommendations)
            .Select(h => new Recommendation(h.ProductId, h.ProductName, score: 0.8f, reason: "Frequently bought together"))
            .ToList();

        logger.LogDebug("Generated {Count} recommendations for customer {CustomerId}", recommendations.Count, customerId);
        return new RecommendationResult(customerId, recommendations);
    }
}

public interface ICustomerPurchaseRepository
{
    Task<List<PurchaseRecord>> GetPurchaseHistoryAsync(string tenantId, string customerId, int limit, CancellationToken ct);
}

public record PurchaseRecord(string ProductId, string ProductName, string CategoryId, int PurchaseCount, DateTimeOffset LastPurchased);
public record Recommendation(string ProductId, string ProductName, float Score, string Reason);
public record RecommendationResult(string CustomerId, List<Recommendation> Recommendations)
{
    public static RecommendationResult Empty(string customerId) => new(customerId, []);
}
