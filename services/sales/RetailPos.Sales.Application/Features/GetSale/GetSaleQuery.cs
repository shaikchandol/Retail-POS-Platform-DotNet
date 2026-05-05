using RetailPos.BuildingBlocks.Application;
using RetailPos.BuildingBlocks.MultiTenancy;
using RetailPos.Sales.Application.Projections;
using RetailPos.Sales.Domain.Exceptions;

namespace RetailPos.Sales.Application.Features.GetSale;

// ── Query ─────────────────────────────────────────────────────────────────────
public record GetSaleQuery(Guid SaleId) : IQuery<Result<SaleReadModel>>;

// ── Handler (reads from Read Model, NOT event store) ─────────────────────────
public class GetSaleHandler(
    ISaleReadModelRepository readRepo,
    ITenantContext tenantContext) : IQueryHandler<GetSaleQuery, Result<SaleReadModel>>
{
    public async Task<Result<SaleReadModel>> Handle(GetSaleQuery query, CancellationToken ct)
    {
        var sale = await readRepo.GetByIdAsync(query.SaleId, tenantContext.TenantId, ct);
        if (sale is null)
            return Result<SaleReadModel>.Failure($"Sale '{query.SaleId}' not found.", "NOT_FOUND");

        return Result<SaleReadModel>.Success(sale);
    }
}

// ── List Query ────────────────────────────────────────────────────────────────
public record GetSalesHistoryQuery(
    string? StoreId = null,
    string? TerminalId = null,
    DateTimeOffset? From = null,
    DateTimeOffset? To = null,
    int Page = 1,
    int Limit = 50) : IQuery<Result<PagedResult<SaleReadModel>>>;

public class GetSalesHistoryHandler(
    ISaleReadModelRepository readRepo,
    ITenantContext tenantContext) : IQueryHandler<GetSalesHistoryQuery, Result<PagedResult<SaleReadModel>>>
{
    public async Task<Result<PagedResult<SaleReadModel>>> Handle(GetSalesHistoryQuery query, CancellationToken ct)
    {
        var (items, total) = await readRepo.ListAsync(
            tenantContext.TenantId, query.StoreId, query.TerminalId,
            query.From, query.To, query.Page, query.Limit, ct);

        return Result<PagedResult<SaleReadModel>>.Success(new(items, total, query.Page, query.Limit));
    }
}

public record PagedResult<T>(IEnumerable<T> Items, int Total, int Page, int Limit)
{
    public int TotalPages => (int)Math.Ceiling((double)Total / Limit);
}
