using RetailPos.Sales.Domain.Aggregates;
using RetailPos.Sales.Domain.Events;
using RetailPos.Sales.Domain.Exceptions;
using RetailPos.Sales.Domain.ValueObjects;
using Xunit;

namespace RetailPos.Sales.UnitTests.Aggregates;

public class SalesTransactionTests
{
    private static (SalesTransaction Sale, string CorrelationId) CreateActiveSale()
    {
        var correlationId = Guid.NewGuid().ToString();
        var sale = SalesTransaction.Initiate(
            Guid.NewGuid(), "tenant-acme", "customer-001",
            "store-42", "terminal-07", "cashier-01", "USD", correlationId);
        return (sale, correlationId);
    }

    private static CartItem MakeItem(string productId = "prod-001", int qty = 1, decimal price = 10m) =>
        CartItem.Create(productId, "Widget", "WGT-001", qty, Money.Of(price), Money.Of(price * 0.1m));

    [Fact]
    public void Initiate_ShouldRaiseSaleInitiatedEvent()
    {
        var (sale, _) = CreateActiveSale();

        Assert.Single(sale.DomainEvents);
        Assert.IsType<SaleInitiatedEvent>(sale.DomainEvents[0]);
        Assert.Equal(SaleStatus.Active, sale.Status);
    }

    [Fact]
    public void AddItem_ShouldRaiseSaleItemAddedEvent()
    {
        var (sale, correlationId) = CreateActiveSale();
        sale.ClearDomainEvents();
        sale.AddItem(MakeItem(), correlationId);

        Assert.Single(sale.DomainEvents);
        Assert.IsType<SaleItemAddedEvent>(sale.DomainEvents[0]);
        Assert.Single(sale.Items);
    }

    [Fact]
    public void AddItem_SameProduct_ShouldUpdateQuantity()
    {
        var (sale, correlationId) = CreateActiveSale();
        sale.AddItem(MakeItem("prod-001", 2), correlationId);
        sale.ClearDomainEvents();
        sale.AddItem(MakeItem("prod-001", 3), correlationId);

        Assert.Single(sale.Items);
        Assert.Equal(5, sale.Items[0].Quantity);
    }

    [Fact]
    public void Complete_WithItems_ShouldRaiseSaleCompletedEvent()
    {
        var (sale, correlationId) = CreateActiveSale();
        sale.AddItem(MakeItem(), correlationId);
        sale.ClearDomainEvents();
        sale.Complete("CARD", "RCP-001", correlationId);

        Assert.Equal(SaleStatus.Completed, sale.Status);
        Assert.IsType<SaleCompletedEvent>(sale.DomainEvents[0]);
    }

    [Fact]
    public void Complete_WithNoItems_ShouldThrow()
    {
        var (sale, correlationId) = CreateActiveSale();
        Assert.Throws<SaleDomainException>(() => sale.Complete("CARD", "RCP-001", correlationId));
    }

    [Fact]
    public void Void_CompletedSale_ShouldThrow()
    {
        var (sale, correlationId) = CreateActiveSale();
        sale.AddItem(MakeItem(), correlationId);
        sale.Complete("CARD", "RCP-001", correlationId);
        Assert.Throws<SaleDomainException>(() => sale.Void("Test", null, correlationId));
    }

    [Fact]
    public void ApplyDiscount_ShouldAffectGrandTotal()
    {
        var (sale, correlationId) = CreateActiveSale();
        sale.AddItem(CartItem.Create("p1", "Widget", "WGT", 1, Money.Of(100m)), correlationId);
        sale.ApplyDiscount("SAVE10", 10, correlationId);

        Assert.Equal(99m, sale.GrandTotal.Amount);  // 100 + 10 tax - 10 discount
    }

    [Fact]
    public void LoadFromHistory_ShouldReconstituteSale()
    {
        var (original, correlationId) = CreateActiveSale();
        original.AddItem(MakeItem(), correlationId);
        original.Complete("CASH", "RCP-001", correlationId);

        var events = original.DomainEvents.ToList();
        var reconstituted = new SalesTransaction();
        reconstituted.LoadFromHistory(events);

        Assert.Equal(SaleStatus.Completed, reconstituted.Status);
        Assert.Single(reconstituted.Items);
    }

    [Fact]
    public void Money_Add_DifferentCurrencies_ShouldThrow()
    {
        var usd = Money.Of(10m, "USD");
        var eur = Money.Of(10m, "EUR");
        Assert.Throws<InvalidOperationException>(() => usd.Add(eur));
    }
}
