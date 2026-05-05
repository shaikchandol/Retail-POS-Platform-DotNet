using MediatR;

namespace RetailPos.BuildingBlocks.Application;

// Commands return a result; use Unit for void commands
public interface ICommand<TResult> : IRequest<TResult> { }
public interface ICommandHandler<TCommand, TResult> : IRequestHandler<TCommand, TResult>
    where TCommand : ICommand<TResult> { }

// Queries always return a result
public interface IQuery<TResult> : IRequest<TResult> { }
public interface IQueryHandler<TQuery, TResult> : IRequestHandler<TQuery, TResult>
    where TQuery : IQuery<TResult> { }

// Event notification (domain event → projection trigger)
public interface IDomainEventNotification<T> : INotification { T DomainEvent { get; } }
public interface IDomainEventHandler<T> : INotificationHandler<T> where T : INotification { }

// Result pattern — avoids exceptions for expected failures
public record Result<T>
{
    public bool IsSuccess { get; init; }
    public T? Value { get; init; }
    public string? Error { get; init; }
    public string? ErrorCode { get; init; }

    public static Result<T> Success(T value) => new() { IsSuccess = true, Value = value };
    public static Result<T> Failure(string error, string? code = null) =>
        new() { IsSuccess = false, Error = error, ErrorCode = code };
}

public record Result
{
    public bool IsSuccess { get; init; }
    public string? Error { get; init; }
    public string? ErrorCode { get; init; }

    public static Result Success() => new() { IsSuccess = true };
    public static Result Failure(string error, string? code = null) =>
        new() { IsSuccess = false, Error = error, ErrorCode = code };
}
