using RetailPos.BuildingBlocks.Domain;

namespace RetailPos.Sales.Domain.ValueObjects;

public sealed class Money : ValueObject
{
    public decimal Amount { get; }
    public string Currency { get; }

    private Money(decimal amount, string currency)
    {
        if (amount < 0) throw new ArgumentException("Amount cannot be negative.", nameof(amount));
        if (string.IsNullOrWhiteSpace(currency)) throw new ArgumentException("Currency is required.", nameof(currency));
        Amount = Math.Round(amount, 2);
        Currency = currency.ToUpperInvariant();
    }

    public static Money Of(decimal amount, string currency = "USD") => new(amount, currency);
    public static Money Zero(string currency = "USD") => new(0, currency);

    public Money Add(Money other)
    {
        GuardSameCurrency(other);
        return new(Amount + other.Amount, Currency);
    }

    public Money Subtract(Money other)
    {
        GuardSameCurrency(other);
        return new(Amount - other.Amount, Currency);
    }

    public Money Multiply(int quantity) => new(Amount * quantity, Currency);
    public Money Multiply(decimal factor) => new(Amount * factor, Currency);

    public bool IsGreaterThan(Money other) { GuardSameCurrency(other); return Amount > other.Amount; }

    private void GuardSameCurrency(Money other)
    {
        if (Currency != other.Currency)
            throw new InvalidOperationException($"Cannot operate on different currencies: {Currency} vs {other.Currency}");
    }

    protected override IEnumerable<object?> GetEqualityComponents() { yield return Amount; yield return Currency; }
    public override string ToString() => $"{Currency} {Amount:F2}";
}
