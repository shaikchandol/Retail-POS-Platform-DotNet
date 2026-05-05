using Dapr.Workflow;
using RetailPos.Sagas.Checkout.States;

namespace RetailPos.Sagas.Checkout.Activities;

/// <summary>
/// Each activity is:
/// - Idempotent: safe to retry (uses sagaId as idempotency key)
/// - Atomic: single unit of work
/// - Compensatable: has a corresponding compensating activity
/// - Observable: emits structured logs + OpenTelemetry spans
/// </summary>

public class ReserveInventoryActivity(IInventorySagaClient client) : WorkflowActivity<ReserveInventoryInput, InventoryReservation>
{
    public override async Task<InventoryReservation> RunAsync(WorkflowActivityContext ctx, ReserveInventoryInput input)
    {
        var result = await client.ReserveStockAsync(
            input.TenantId, input.ProductId, input.Quantity,
            idempotencyKey: $"saga-reserve-{input.SagaId}-{input.ProductId}");

        return new InventoryReservation(result.Success, result.ReservationId ?? string.Empty);
    }
}

public class ReleaseInventoryActivity(IInventorySagaClient client) : WorkflowActivity<ReleaseInventoryInput, bool>
{
    public override async Task<bool> RunAsync(WorkflowActivityContext ctx, ReleaseInventoryInput input)
    {
        await client.ReleaseReservationAsync(input.TenantId, input.ReservationId, input.Reason,
            idempotencyKey: $"saga-release-{input.SagaId}-{input.ReservationId}");
        return true;
    }
}

public class AuthorisePaymentActivity(IPaymentSagaClient client) : WorkflowActivity<AuthorisePaymentInput, PaymentResult>
{
    public override async Task<PaymentResult> RunAsync(WorkflowActivityContext ctx, AuthorisePaymentInput input)
    {
        var result = await client.AuthoriseAsync(
            input.TenantId, input.SaleId, input.Amount, input.Currency, input.Method,
            idempotencyKey: $"saga-payment-{input.SagaId}");

        return new PaymentResult(result.Authorised, result.AuthCode, result.DeclineReason);
    }
}

public class CompleteSaleActivity(ISalesSagaClient client) : WorkflowActivity<CompleteSaleInput, bool>
{
    public override async Task<bool> RunAsync(WorkflowActivityContext ctx, CompleteSaleInput input)
    {
        await client.CompleteSaleAsync(input.TenantId, input.SaleId, input.AuthCode,
            idempotencyKey: $"saga-complete-{input.SagaId}");
        return true;
    }
}

public class ConfirmInventoryActivity(IInventorySagaClient client) : WorkflowActivity<ConfirmInventoryInput, bool>
{
    public override async Task<bool> RunAsync(WorkflowActivityContext ctx, ConfirmInventoryInput input)
    {
        await client.ConfirmReservationAsync(input.TenantId, input.ReservationId,
            idempotencyKey: $"saga-confirm-{input.SagaId}-{input.ReservationId}");
        return true;
    }
}

// ── Input records ─────────────────────────────────────────────────────────────
public record ReserveInventoryInput(string TenantId, string StoreId, string ProductId, int Quantity, string SagaId);
public record ReleaseInventoryInput(string TenantId, string ReservationId, string Reason, string SagaId);
public record AuthorisePaymentInput(string TenantId, Guid SaleId, decimal Amount, string Currency, string Method, string SagaId);
public record CompleteSaleInput(string TenantId, Guid SaleId, string AuthCode, string SagaId);
public record ConfirmInventoryInput(string TenantId, string ReservationId, string SagaId);

// ── Saga client interfaces (implemented by typed HTTP clients) ─────────────────
public interface IInventorySagaClient
{
    Task<(bool Success, string? ReservationId)> ReserveStockAsync(string tenantId, string productId, int qty, string idempotencyKey);
    Task ReleaseReservationAsync(string tenantId, string reservationId, string reason, string idempotencyKey);
    Task ConfirmReservationAsync(string tenantId, string reservationId, string idempotencyKey);
}
public interface IPaymentSagaClient
{
    Task<(bool Authorised, string? AuthCode, string? DeclineReason)> AuthoriseAsync(string tenantId, Guid saleId, decimal amount, string currency, string method, string idempotencyKey);
}
public interface ISalesSagaClient
{
    Task CompleteSaleAsync(string tenantId, Guid saleId, string authCode, string idempotencyKey);
}
