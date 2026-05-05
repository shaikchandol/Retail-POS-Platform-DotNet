using RetailPos.BuildingBlocks.Application;
using RetailPos.BuildingBlocks.Dapr;
using RetailPos.BuildingBlocks.EventSourcing;
using RetailPos.BuildingBlocks.MultiTenancy;
using RetailPos.Orders.Domain.Aggregates;

namespace RetailPos.Orders.Application.Features.CreateOrder;

public record CreateOrderCommand(Guid SaleId, string CustomerId, List<OrderLineRequest> Lines) : ICommand<Result<Guid>>;
public record OrderLineRequest(string ProductId, string Sku, int Quantity, decimal UnitPrice);

public class CreateOrderHandler(IEventStore eventStore, IDaprEventPublisher publisher, ITenantContext tc)
    : ICommandHandler<CreateOrderCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreateOrderCommand cmd, CancellationToken ct)
    {
        var orderId = Guid.NewGuid();
        var lines = cmd.Lines.Select(l => new OrderLine(l.ProductId, l.Sku, l.Quantity, l.UnitPrice)).ToList();
        var order = Order.Create(orderId, cmd.SaleId, tc.TenantId, cmd.CustomerId, lines, Guid.NewGuid().ToString());

        await eventStore.SaveEventsAsync($"order-{orderId}", order.DomainEvents, expectedVersion: -1, ct);
        foreach (var evt in order.DomainEvents.OfType<IVersionedEvent>())
            await publisher.PublishAsync(evt, ct: ct);

        order.ClearDomainEvents();
        return Result<Guid>.Success(orderId);
    }
}
