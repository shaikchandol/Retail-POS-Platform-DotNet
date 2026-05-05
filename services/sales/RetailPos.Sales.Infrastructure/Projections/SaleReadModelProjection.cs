using Dapr;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using RetailPos.Sales.Application.Projections;
using RetailPos.Sales.Domain.Events;

namespace RetailPos.Sales.Infrastructure.Projections;

/// <summary>
/// Dapr subscriber — consumes sale events from Kafka via Dapr pub/sub
/// and projects them into the read model (eventually consistent).
///
/// Idempotency: upserts by SaleId — safe to replay.
/// </summary>
[ApiController]
[Route("subscriptions")]
public class SaleProjectionController(
    ISaleReadModelRepository repo,
    ILogger<SaleProjectionController> logger) : ControllerBase
{
    [Topic("kafka-pubsub", "sale.initiated")]
    [HttpPost("sale-initiated")]
    public async Task<IActionResult> OnSaleInitiated([FromBody] SaleInitiatedEvent evt, CancellationToken ct)
    {
        logger.LogInformation("Projecting {EventType} for Sale {SaleId}", evt.EventType, evt.SaleId);
        var rm = new SaleReadModel
        {
            Id = evt.SaleId, TenantId = evt.TenantId, StoreId = evt.StoreId ?? string.Empty,
            TerminalId = evt.TerminalId ?? string.Empty, CustomerId = evt.CustomerId,
            CashierId = evt.CashierId ?? string.Empty, Status = "Active", Currency = evt.Currency,
            CreatedAt = evt.OccurredAt, UpdatedAt = evt.OccurredAt, Version = 1
        };
        await repo.UpsertAsync(rm, ct);
        return Ok();
    }

    [Topic("kafka-pubsub", "sale.item-added")]
    [HttpPost("sale-item-added")]
    public async Task<IActionResult> OnItemAdded([FromBody] SaleItemAddedEvent evt, CancellationToken ct)
    {
        var rm = await repo.GetByIdAsync(evt.SaleId, evt.TenantId, ct);
        if (rm is null) return Ok();    // idempotency — event may arrive before initiated in edge cases

        rm.Items.Add(new SaleItemReadModel
        {
            ProductId = evt.ProductId, ProductName = evt.ProductName, Sku = evt.Sku,
            Quantity = evt.Quantity, UnitPrice = evt.UnitPrice, TaxAmount = evt.TaxAmount,
            DiscountAmount = evt.DiscountAmount,
            LineTotal = (evt.UnitPrice * evt.Quantity) + evt.TaxAmount - evt.DiscountAmount,
        });
        RecalculateTotals(rm);
        rm.UpdatedAt = evt.OccurredAt;
        await repo.UpsertAsync(rm, ct);
        return Ok();
    }

    [Topic("kafka-pubsub", "sale.completed")]
    [HttpPost("sale-completed")]
    public async Task<IActionResult> OnSaleCompleted([FromBody] SaleCompletedEvent evt, CancellationToken ct)
    {
        var rm = await repo.GetByIdAsync(evt.SaleId, evt.TenantId, ct);
        if (rm is null) return Ok();

        rm.Status = "Completed";
        rm.GrandTotal = evt.TotalAmount;
        rm.TaxTotal = evt.TaxTotal;
        rm.DiscountTotal = evt.DiscountTotal;
        rm.PaymentMethod = evt.PaymentMethod;
        rm.ReceiptNumber = evt.ReceiptNumber;
        rm.UpdatedAt = evt.OccurredAt;
        await repo.UpsertAsync(rm, ct);
        return Ok();
    }

    [Topic("kafka-pubsub", "sale.voided")]
    [HttpPost("sale-voided")]
    public async Task<IActionResult> OnSaleVoided([FromBody] SaleVoidedEvent evt, CancellationToken ct)
    {
        var rm = await repo.GetByIdAsync(evt.SaleId, evt.TenantId, ct);
        if (rm is null) return Ok();
        rm.Status = "Voided"; rm.UpdatedAt = evt.OccurredAt;
        await repo.UpsertAsync(rm, ct);
        return Ok();
    }

    private static void RecalculateTotals(SaleReadModel rm)
    {
        rm.SubTotal = rm.Items.Sum(i => i.UnitPrice * i.Quantity);
        rm.TaxTotal = rm.Items.Sum(i => i.TaxAmount);
        rm.DiscountTotal = rm.Items.Sum(i => i.DiscountAmount);
        rm.GrandTotal = rm.SubTotal + rm.TaxTotal - rm.DiscountTotal;
    }
}
