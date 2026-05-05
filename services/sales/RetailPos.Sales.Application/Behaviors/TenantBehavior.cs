using MediatR;
using Microsoft.Extensions.Logging;
using RetailPos.BuildingBlocks.MultiTenancy;

namespace RetailPos.Sales.Application.Behaviors;

/// <summary>
/// MediatR pipeline behavior — validates tenant context before every command/query.
/// </summary>
public class TenantValidationBehavior<TRequest, TResponse>(
    ITenantContext tenantContext,
    ILogger<TenantValidationBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse> where TRequest : notnull
{
    public Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        if (!tenantContext.IsResolved)
            throw new InvalidOperationException("Tenant context is not resolved. Ensure TenantMiddleware is registered.");

        logger.LogDebug("Executing {RequestType} for Tenant={TenantId} Store={StoreId}",
            typeof(TRequest).Name, tenantContext.TenantId, tenantContext.StoreId);

        return next(ct);
    }
}

/// <summary>
/// Logging + timing pipeline behavior.
/// </summary>
public class LoggingBehavior<TRequest, TResponse>(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse> where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        logger.LogInformation("→ {Request}", typeof(TRequest).Name);
        var result = await next(ct);
        sw.Stop();
        logger.LogInformation("← {Request} in {Elapsed}ms", typeof(TRequest).Name, sw.ElapsedMilliseconds);
        return result;
    }
}

/// <summary>
/// FluentValidation pipeline behavior.
/// </summary>
public class ValidationBehavior<TRequest, TResponse>(IEnumerable<FluentValidation.IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse> where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        var failures = validators
            .Select(v => v.Validate(request))
            .SelectMany(r => r.Errors)
            .Where(f => f != null)
            .ToList();

        if (failures.Any())
            throw new FluentValidation.ValidationException(failures);

        return await next(ct);
    }
}
