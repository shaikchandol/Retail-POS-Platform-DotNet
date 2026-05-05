using RetailPos.BuildingBlocks.Application;
using RetailPos.BuildingBlocks.Dapr;
using RetailPos.BuildingBlocks.EventSourcing;
using RetailPos.BuildingBlocks.MultiTenancy;
using RetailPos.Sales.Domain.Aggregates;
using RetailPos.Sales.Domain.Events;
using RetailPos.Sales.Domain.Exceptions;

namespace RetailPos.Sales.Application.Features.VoidSale;

public record VoidSaleCommand(Guid SaleId, string Reason, string? AuthorizedBy) : ICommand<Result>;

public class VoidSaleHandler(
    IEventStore eventStore,
    IDaprEventPublisher publisher,
    ITenantContext tenantContext) : ICommandHandler<VoidSaleCommand, Result>
{
    public async Task<Result> Handle(VoidSaleCommand cmd, CancellationToken ct)
    {
        var streamId = $"sale-{cmd.SaleId}";

        if (!await eventStore.StreamExistsAsync(streamId, ct))
            return Result.Failure($"Sale '{cmd.SaleId}' not found.", "NOT_FOUND");

        var events = await eventStore.GetEventsAsync(streamId, ct: ct);
        var sale = new SalesTransaction();
        sale.LoadFromHistory(events);

        sale.Void(cmd.Reason, cmd.AuthorizedBy, correlationId: Guid.NewGuid().ToString());
        await eventStore.SaveEventsAsync(streamId, sale.DomainEvents, sale.Version, ct);

        foreach (var evt in sale.DomainEvents.OfType<IVersionedEvent>())
            await publisher.PublishAsync(evt, ct: ct);

        sale.ClearDomainEvents();
        return Result.Success();
    }
}
