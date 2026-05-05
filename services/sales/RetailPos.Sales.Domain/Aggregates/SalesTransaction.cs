using RetailPos.BuildingBlocks.EventSourcing;
using RetailPos.Sales.Domain.Events;
using RetailPos.Sales.Domain.Exceptions;
using RetailPos.Sales.Domain.ValueObjects;

namespace RetailPos.Sales.Domain.Aggregates;

/// <summary>
/// SalesTransaction — the core event-sourced aggregate.
/// All state changes happen ONLY through domain events.
/// Never modify state directly; always call RaiseEvent().
/// </summary>
public class SalesTransaction : EventSourcedAggregate<Guid>
{
    // ── State (rebuilt by When() methods) ────────────────────────────────────
    public string TenantId { get; private set; } = string.Empty;
    public string CustomerId { get; private set; } = string.Empty;
    public string StoreId { get; private set; } = string.Empty;
    public string TerminalId { get; private set; } = string.Empty;
    public SaleStatus Status { get; private set; }
    public string Currency { get; private set; } = "USD";
    public List<CartItem> Items { get; private set; } = [];
    public decimal DiscountPercentage { get; private set; }
    public string? CompletedReceiptNumber { get; private set; }

    // Computed
    public Money SubTotal => Items.Aggregate(Money.Zero(Currency), (acc, i) => acc.Add(i.UnitPrice.Multiply(i.Quantity)));
    public Money TaxTotal => Items.Aggregate(Money.Zero(Currency), (acc, i) => acc.Add(i.TaxAmount));
    public Money DiscountTotal => SubTotal.Multiply(DiscountPercentage / 100);
    public Money GrandTotal => SubTotal.Add(TaxTotal).Subtract(DiscountTotal);

    private SalesTransaction() { }

    // ── Factory ───────────────────────────────────────────────────────────────
    public static SalesTransaction Initiate(
        Guid saleId, string tenantId, string customerId, string storeId,
        string terminalId, string cashierId, string currency, string correlationId)
    {
        var sale = new SalesTransaction();
        sale.RaiseEvent(new SaleInitiatedEvent
        {
            SaleId = saleId,
            TenantId = tenantId,
            CustomerId = customerId,
            Currency = currency,
            StoreId = storeId,
            TerminalId = terminalId,
            CashierId = cashierId,
            CorrelationId = correlationId
        });
        return sale;
    }

    // ── Commands ──────────────────────────────────────────────────────────────
    public void AddItem(CartItem item, string correlationId)
    {
        GuardActive();
        var existing = Items.FirstOrDefault(i => i.ProductId == item.ProductId);
        if (existing is not null)
        {
            UpdateItemQuantity(item.ProductId, existing.Quantity + item.Quantity, correlationId);
            return;
        }
        RaiseEvent(new SaleItemAddedEvent
        {
            SaleId = Id,
            TenantId = TenantId,
            ProductId = item.ProductId,
            ProductName = item.ProductName,
            Sku = item.Sku,
            Quantity = item.Quantity,
            UnitPrice = item.UnitPrice.Amount,
            TaxAmount = item.TaxAmount.Amount,
            DiscountAmount = item.DiscountAmount.Amount,
            Currency = Currency,
            CorrelationId = correlationId,
            StoreId = StoreId,
            TerminalId = TerminalId
        });
    }

    public void RemoveItem(string productId, string correlationId)
    {
        GuardActive();
        if (!Items.Any(i => i.ProductId == productId))
            throw new SaleDomainException($"Product '{productId}' not found in sale.");
        RaiseEvent(new SaleItemRemovedEvent { SaleId = Id, TenantId = TenantId, ProductId = productId, CorrelationId = correlationId });
    }

    public void UpdateItemQuantity(string productId, int newQty, string correlationId)
    {
        GuardActive();
        if (newQty <= 0) { RemoveItem(productId, correlationId); return; }
        RaiseEvent(new SaleItemQuantityUpdatedEvent { SaleId = Id, TenantId = TenantId, ProductId = productId, NewQuantity = newQty, CorrelationId = correlationId });
    }

    public void ApplyDiscount(string discountCode, decimal percentage, string correlationId)
    {
        GuardActive();
        if (percentage < 0 || percentage > 100)
            throw new SaleDomainException("Discount percentage must be between 0 and 100.");
        RaiseEvent(new DiscountAppliedEvent { SaleId = Id, TenantId = TenantId, DiscountCode = discountCode, DiscountPercentage = percentage, CorrelationId = correlationId });
    }

    public void Complete(string paymentMethod, string receiptNumber, string correlationId)
    {
        GuardActive();
        if (!Items.Any()) throw new SaleDomainException("Cannot complete a sale with no items.");
        RaiseEvent(new SaleCompletedEvent
        {
            SaleId = Id, TenantId = TenantId, TotalAmount = GrandTotal.Amount, TaxTotal = TaxTotal.Amount,
            DiscountTotal = DiscountTotal.Amount, Currency = Currency, PaymentMethod = paymentMethod,
            ReceiptNumber = receiptNumber, CorrelationId = correlationId, StoreId = StoreId, TerminalId = TerminalId
        });
    }

    public void Void(string reason, string? authorizedBy, string correlationId)
    {
        if (Status == SaleStatus.Completed)
            throw new SaleDomainException("Completed sales must be refunded, not voided.");
        if (Status == SaleStatus.Voided)
            throw new SaleDomainException("Sale is already voided.");
        RaiseEvent(new SaleVoidedEvent { SaleId = Id, TenantId = TenantId, Reason = reason, AuthorizedBy = authorizedBy, CorrelationId = correlationId });
    }

    // ── When (State Mutators) ─────────────────────────────────────────────────
    public void When(SaleInitiatedEvent e)
    {
        Id = e.SaleId; TenantId = e.TenantId; CustomerId = e.CustomerId;
        StoreId = e.StoreId ?? string.Empty; TerminalId = e.TerminalId ?? string.Empty;
        Currency = e.Currency; Status = SaleStatus.Active; Items = [];
    }

    public void When(SaleItemAddedEvent e) =>
        Items.Add(CartItem.Create(e.ProductId, e.ProductName, e.Sku, e.Quantity,
            Money.Of(e.UnitPrice, e.Currency), Money.Of(e.TaxAmount, e.Currency), Money.Of(e.DiscountAmount, e.Currency)));

    public void When(SaleItemRemovedEvent e) =>
        Items.RemoveAll(i => i.ProductId == e.ProductId);

    public void When(SaleItemQuantityUpdatedEvent e)
    {
        var item = Items.First(i => i.ProductId == e.ProductId);
        Items.Remove(item);
        Items.Add(item.WithQuantity(e.NewQuantity));
    }

    public void When(DiscountAppliedEvent e) => DiscountPercentage = e.DiscountPercentage;

    public void When(SaleCompletedEvent e) { Status = SaleStatus.Completed; CompletedReceiptNumber = e.ReceiptNumber; }

    public void When(SaleVoidedEvent e) => Status = SaleStatus.Voided;

    public void When(SaleRefundedEvent e) => Status = SaleStatus.Refunded;

    // ── Guards ────────────────────────────────────────────────────────────────
    private void GuardActive()
    {
        if (Status != SaleStatus.Active)
            throw new SaleDomainException($"Cannot modify sale in '{Status}' status.");
    }
}

public enum SaleStatus { Active, Completed, Voided, Refunded }
