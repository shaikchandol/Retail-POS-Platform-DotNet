using FluentValidation;
using MediatR;
using RetailPos.BuildingBlocks.Application;
using RetailPos.BuildingBlocks.Dapr;
using RetailPos.BuildingBlocks.EventSourcing;
using RetailPos.BuildingBlocks.MultiTenancy;
using RetailPos.Sales.Domain.Aggregates;
using RetailPos.Sales.Domain.Events;
using RetailPos.Sales.Domain.ValueObjects;

namespace RetailPos.Sales.Application.Features.CreateSale;

// ── Command ───────────────────────────────────────────────────────────────────
public record CreateSaleCommand : ICommand<Result<CreateSaleResponse>>
{
    public required string CustomerId { get; init; }
    public required string PaymentMethod { get; init; }
    public required List<SaleItemRequest> Items { get; init; }
    public string? DiscountCode { get; init; }
    public decimal? DiscountPercentage { get; init; }
    public string Currency { get; init; } = "USD";
    public string? CorrelationId { get; init; }
}

public record SaleItemRequest(string ProductId, string ProductName, string Sku, int Quantity, decimal UnitPrice, decimal TaxRate);
public record CreateSaleResponse(Guid SaleId, string ReceiptNumber, decimal TotalAmount, string Currency);

// ── Validator ─────────────────────────────────────────────────────────────────
public class CreateSaleValidator : AbstractValidator<CreateSaleCommand>
{
    public CreateSaleValidator()
    {
        RuleFor(x => x.CustomerId).NotEmpty();
        RuleFor(x => x.PaymentMethod).NotEmpty();
        RuleFor(x => x.Items).NotEmpty().WithMessage("A sale must have at least one item.");
        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.ProductId).NotEmpty();
            item.RuleFor(i => i.Quantity).GreaterThan(0);
            item.RuleFor(i => i.UnitPrice).GreaterThanOrEqualTo(0);
            item.RuleFor(i => i.TaxRate).InclusiveBetween(0, 1);
        });
        RuleFor(x => x.DiscountPercentage).InclusiveBetween(0, 100).When(x => x.DiscountPercentage.HasValue);
    }
}

// ── Handler ───────────────────────────────────────────────────────────────────
public class CreateSaleHandler(
    IEventStore eventStore,
    IDaprEventPublisher publisher,
    ITenantContext tenantContext) : ICommandHandler<CreateSaleCommand, Result<CreateSaleResponse>>
{
    public async Task<Result<CreateSaleResponse>> Handle(CreateSaleCommand cmd, CancellationToken ct)
    {
        var saleId = Guid.NewGuid();
        var correlationId = cmd.CorrelationId ?? Guid.NewGuid().ToString();
        var receiptNumber = GenerateReceiptNumber(tenantContext.StoreId, tenantContext.TerminalId);

        // Initiate aggregate
        var sale = SalesTransaction.Initiate(
            saleId, tenantContext.TenantId, cmd.CustomerId,
            tenantContext.StoreId, tenantContext.TerminalId,
            cashierId: string.Empty, cmd.Currency, correlationId);

        // Add items
        foreach (var item in cmd.Items)
        {
            var unitPrice = Money.Of(item.UnitPrice, cmd.Currency);
            var taxAmount = unitPrice.Multiply(item.Quantity).Multiply(item.TaxRate);
            var cartItem = CartItem.Create(item.ProductId, item.ProductName, item.Sku, item.Quantity, unitPrice, taxAmount);
            sale.AddItem(cartItem, correlationId);
        }

        // Apply discount
        if (!string.IsNullOrWhiteSpace(cmd.DiscountCode) && cmd.DiscountPercentage > 0)
            sale.ApplyDiscount(cmd.DiscountCode, cmd.DiscountPercentage!.Value, correlationId);

        // Complete sale
        sale.Complete(cmd.PaymentMethod, receiptNumber, correlationId);

        // Persist events
        var streamId = $"sale-{saleId}";
        await eventStore.SaveEventsAsync(streamId, sale.DomainEvents, expectedVersion: -1, ct);

        // Publish events via Dapr → Kafka
        foreach (var domainEvent in sale.DomainEvents.OfType<IVersionedEvent>())
            await publisher.PublishAsync(domainEvent, ct: ct);

        sale.ClearDomainEvents();

        return Result<CreateSaleResponse>.Success(new CreateSaleResponse(saleId, receiptNumber, sale.GrandTotal.Amount, cmd.Currency));
    }

    private static string GenerateReceiptNumber(string storeId, string terminalId) =>
        $"RCP-{storeId.ToUpperInvariant()}-{terminalId.ToUpperInvariant()}-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString()[..6].ToUpperInvariant()}";
}
