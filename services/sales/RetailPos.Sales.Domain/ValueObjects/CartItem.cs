using RetailPos.BuildingBlocks.Domain;

namespace RetailPos.Sales.Domain.ValueObjects;

public sealed class CartItem : ValueObject
{
    public string ProductId { get; }
    public string ProductName { get; }
    public string Sku { get; }
    public int Quantity { get; }
    public Money UnitPrice { get; }
    public Money TaxAmount { get; }
    public Money DiscountAmount { get; }
    public Money LineTotal => UnitPrice.Multiply(Quantity).Add(TaxAmount).Subtract(DiscountAmount);

    private CartItem(string productId, string productName, string sku, int quantity, Money unitPrice, Money taxAmount, Money discountAmount)
    {
        if (quantity <= 0) throw new ArgumentException("Quantity must be positive.", nameof(quantity));
        ProductId = productId;
        ProductName = productName;
        Sku = sku;
        Quantity = quantity;
        UnitPrice = unitPrice;
        TaxAmount = taxAmount;
        DiscountAmount = discountAmount;
    }

    public static CartItem Create(string productId, string productName, string sku, int quantity, Money unitPrice, Money? taxAmount = null, Money? discountAmount = null) =>
        new(productId, productName, sku, quantity, unitPrice,
            taxAmount ?? Money.Zero(unitPrice.Currency),
            discountAmount ?? Money.Zero(unitPrice.Currency));

    public CartItem WithQuantity(int quantity) => new(ProductId, ProductName, Sku, quantity, UnitPrice, TaxAmount, DiscountAmount);
    public CartItem WithDiscount(Money discount) => new(ProductId, ProductName, Sku, Quantity, UnitPrice, TaxAmount, discount);

    protected override IEnumerable<object?> GetEqualityComponents()
    { yield return ProductId; yield return Sku; yield return Quantity; yield return UnitPrice; }
}
