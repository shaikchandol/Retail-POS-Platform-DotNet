using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RetailPos.BuildingBlocks.EventSourcing;
using RetailPos.Sales.Application.Projections;
using RetailPos.Sales.Domain.Events;

namespace RetailPos.Sales.Worker;

/// <summary>
/// ASP.NET Core–hosted background worker (not a console app).
/// Continuously processes uncommitted projections and applies them
/// to the read model — a catch-up subscription pattern.
///
/// Complements the Dapr push-based projection for reliability:
/// - If Dapr delivery fails, this worker replays from the event store.
/// - Runs on a configurable polling interval (default: 5s).
/// - Per-tenant: each iteration scans all tenant schemas.
/// </summary>
public class ProjectionWorker(
    IEventStore eventStore,
    ISaleReadModelRepository readRepo,
    IProjectionCheckpointStore checkpointStore,
    ILogger<ProjectionWorker> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("ProjectionWorker started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingProjectionsAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "ProjectionWorker error. Retrying in {Interval}s.", PollInterval.TotalSeconds);
            }

            await Task.Delay(PollInterval, stoppingToken);
        }

        logger.LogInformation("ProjectionWorker stopped.");
    }

    private async Task ProcessPendingProjectionsAsync(CancellationToken ct)
    {
        var checkpoint = await checkpointStore.GetCheckpointAsync("sale-projection", ct);
        var newEvents  = await eventStore.GetEventsFromPositionAsync(checkpoint.LastPosition, limit: 500, ct);

        foreach (var (streamId, evt) in newEvents)
        {
            await ApplyProjectionAsync(streamId, evt, ct);
            checkpoint = checkpoint with { LastPosition = checkpoint.LastPosition + 1 };
        }

        if (newEvents.Any())
        {
            await checkpointStore.SaveCheckpointAsync("sale-projection", checkpoint, ct);
            logger.LogInformation("ProjectionWorker applied {Count} events up to position {Position}",
                newEvents.Count, checkpoint.LastPosition);
        }
    }

    private async Task ApplyProjectionAsync(string streamId, IDomainEvent evt, CancellationToken ct)
    {
        // Extract saleId from stream (e.g., "sale-{guid}")
        if (!streamId.StartsWith("sale-")) return;
        var saleId = Guid.Parse(streamId["sale-".Length..]);

        switch (evt)
        {
            case SaleInitiatedEvent e:
                await readRepo.UpsertAsync(new SaleReadModel
                {
                    Id = e.SaleId, TenantId = e.TenantId, StoreId = e.StoreId ?? string.Empty,
                    TerminalId = e.TerminalId ?? string.Empty, CustomerId = e.CustomerId,
                    Status = "Active", Currency = e.Currency, CreatedAt = e.OccurredAt, UpdatedAt = e.OccurredAt
                }, ct);
                break;

            case SaleCompletedEvent e:
                var rm = await readRepo.GetByIdAsync(e.SaleId, e.TenantId, ct);
                if (rm is not null)
                {
                    rm.Status = "Completed"; rm.GrandTotal = e.TotalAmount;
                    rm.TaxTotal = e.TaxTotal; rm.PaymentMethod = e.PaymentMethod;
                    rm.ReceiptNumber = e.ReceiptNumber; rm.UpdatedAt = e.OccurredAt;
                    await readRepo.UpsertAsync(rm, ct);
                }
                break;

            case SaleVoidedEvent e:
                var rmv = await readRepo.GetByIdAsync(e.SaleId, e.TenantId, ct);
                if (rmv is not null)
                { rmv.Status = "Voided"; rmv.UpdatedAt = e.OccurredAt; await readRepo.UpsertAsync(rmv, ct); }
                break;
        }
    }
}

public interface IProjectionCheckpointStore
{
    Task<ProjectionCheckpoint> GetCheckpointAsync(string projectionId, CancellationToken ct);
    Task SaveCheckpointAsync(string projectionId, ProjectionCheckpoint checkpoint, CancellationToken ct);
}

public record ProjectionCheckpoint(string ProjectionId, long LastPosition);
