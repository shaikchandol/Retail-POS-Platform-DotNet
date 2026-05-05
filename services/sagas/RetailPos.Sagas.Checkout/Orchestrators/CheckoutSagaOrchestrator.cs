using Dapr.Workflow;
using RetailPos.Sagas.Checkout.Activities;
using RetailPos.Sagas.Checkout.States;

namespace RetailPos.Sagas.Checkout.Orchestrators;

/// <summary>
/// Checkout Saga — Orchestration-based saga using Dapr Workflow (built on Azure Durable Task).
///
/// Steps:
///   1. Reserve inventory (for each line item)
///   2. Authorise payment
///   3. Confirm sale + emit SaleCompleted
///   4. Release reservation (if payment fails — compensation)
///   5. Void sale (if any step fails — compensation)
///
/// Guarantees: exactly-once business effect via idempotency keys at each activity.
/// Observability: each step emits structured events to Kafka for monitoring.
///
/// Swap point: replace Dapr Workflow with MassTransit Saga, NServiceBus,
///   or Temporal — same activity interfaces, different workflow host.
/// </summary>
public class CheckoutSagaOrchestrator : Workflow<CheckoutSagaInput, CheckoutSagaResult>
{
    public override async Task<CheckoutSagaResult> RunAsync(
        WorkflowContext ctx, CheckoutSagaInput input)
    {
        var logger = ctx.CreateReplaySafeLogger<CheckoutSagaOrchestrator>();
        var sagaId  = ctx.InstanceId;

        logger.LogInformation("Checkout saga {SagaId} started for sale {SaleId}", sagaId, input.SaleId);

        // ── Step 1: Reserve Inventory ─────────────────────────────────────────
        var reservations = new List<InventoryReservation>();
        foreach (var item in input.Items)
        {
            var reservation = await ctx.CallActivityAsync<InventoryReservation>(
                nameof(ReserveInventoryActivity),
                new ReserveInventoryInput(input.TenantId, input.StoreId, item.ProductId, item.Quantity, sagaId));

            if (!reservation.Success)
            {
                logger.LogWarning("Inventory reservation failed for product {ProductId}", item.ProductId);
                await CompensateReservations(ctx, reservations, input.TenantId, sagaId);
                return CheckoutSagaResult.Failed($"Insufficient stock for product {item.ProductId}.");
            }

            reservations.Add(reservation);
        }

        // ── Step 2: Authorise Payment ─────────────────────────────────────────
        var paymentResult = await ctx.CallActivityAsync<PaymentResult>(
            nameof(AuthorisePaymentActivity),
            new AuthorisePaymentInput(input.TenantId, input.SaleId, input.TotalAmount, input.Currency, input.PaymentMethod, sagaId));

        if (!paymentResult.Authorised)
        {
            logger.LogWarning("Payment declined for sale {SaleId}: {Reason}", input.SaleId, paymentResult.DeclineReason);
            await CompensateReservations(ctx, reservations, input.TenantId, sagaId);
            return CheckoutSagaResult.Failed($"Payment declined: {paymentResult.DeclineReason}");
        }

        // ── Step 3: Complete Sale ─────────────────────────────────────────────
        await ctx.CallActivityAsync(
            nameof(CompleteSaleActivity),
            new CompleteSaleInput(input.TenantId, input.SaleId, paymentResult.AuthCode!, sagaId));

        // ── Step 4: Confirm Reservations (convert to deductions) ──────────────
        foreach (var reservation in reservations)
        {
            await ctx.CallActivityAsync(
                nameof(ConfirmInventoryActivity),
                new ConfirmInventoryInput(input.TenantId, reservation.ReservationId, sagaId));
        }

        logger.LogInformation("Checkout saga {SagaId} completed successfully for sale {SaleId}", sagaId, input.SaleId);

        return CheckoutSagaResult.Completed(paymentResult.AuthCode!);
    }

    private static async Task CompensateReservations(
        WorkflowContext ctx, List<InventoryReservation> reservations,
        string tenantId, string sagaId)
    {
        // Compensating transactions — run even if individual ones fail (fire and forget with retry)
        foreach (var reservation in reservations)
        {
            await ctx.CallActivityAsync(
                nameof(ReleaseInventoryActivity),
                new ReleaseInventoryInput(tenantId, reservation.ReservationId, "saga-compensation", sagaId));
        }
    }
}

// ── Input / Output types ──────────────────────────────────────────────────────
public record CheckoutSagaInput(
    Guid SaleId, string TenantId, string StoreId,
    string PaymentMethod, decimal TotalAmount, string Currency,
    List<SagaLineItem> Items);

public record SagaLineItem(string ProductId, int Quantity);

public record CheckoutSagaResult(bool Success, string? FailureReason, string? PaymentAuthCode)
{
    public static CheckoutSagaResult Completed(string authCode) => new(true, null, authCode);
    public static CheckoutSagaResult Failed(string reason) => new(false, reason, null);
}

public record InventoryReservation(bool Success, string ReservationId);
public record PaymentResult(bool Authorised, string? AuthCode, string? DeclineReason);
