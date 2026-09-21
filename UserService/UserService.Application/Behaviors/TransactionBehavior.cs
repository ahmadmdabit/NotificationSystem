using MediatR;
using Microsoft.Extensions.Logging;
using UserService.Domain.Abstractions;

namespace UserService.Application.Behaviors;

/// <summary>
/// MediatR pipeline behavior that wraps write commands in a transaction.
/// </summary>
public sealed class TransactionBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<TransactionBehavior<TRequest, TResponse>> _logger;

    public TransactionBehavior(IUnitOfWork unitOfWork, ILogger<TransactionBehavior<TRequest, TResponse>> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        // Only wrap commands (ICommand) in transactions
        if (request is not ICommand)
            return await next().ConfigureAwait(false);

        _logger.LogInformation("[TransactionBehavior] Beginning transaction for {Request}", typeof(TRequest).Name);
        await _unitOfWork.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            var response = await next().ConfigureAwait(false);
            await _unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("[TransactionBehavior] Transaction committed for {Request}", typeof(TRequest).Name);
            return response;
        }
        catch
        {
            _logger.LogWarning("[TransactionBehavior] Rolling back transaction for {Request}", typeof(TRequest).Name);
            await _unitOfWork.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
    }
}

/// <summary>
/// Marker interface for commands that should be wrapped in a transaction.
/// </summary>
public interface ICommand { }
