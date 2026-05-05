namespace RetailPos.Sales.Domain.Exceptions;

public class SaleDomainException : Exception
{
    public SaleDomainException(string message) : base(message) { }
    public SaleDomainException(string message, Exception inner) : base(message, inner) { }
}

public class SaleNotFoundException : Exception
{
    public SaleNotFoundException(Guid saleId) : base($"Sale '{saleId}' was not found.") { }
}

public class ConcurrencyException : Exception
{
    public ConcurrencyException(string streamId, int expected, int actual)
        : base($"Concurrency conflict on stream '{streamId}': expected version {expected}, actual {actual}.") { }
}
