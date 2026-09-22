using MediatR;
using Microsoft.Extensions.Logging;
using Shared.Application.Abstractions;
using Shared.Application.Behaviors;
using Shared.Domain;
using Shared.Domain.Abstractions;

namespace Shared.Application.Behaviors;

/// <summary>
/// MediatR pipeline behavior that wraps write commands (ICommand) in a transaction.
/// Single shared implementation (DRY) — registered by each service's Application DI.
/// <para>
/// Dispatch-after-commit: aggregate domain events raised inside the handler are drained
/// from <see cref="Domain.DomainEventCollector"/> and published via
/// <see cref="Domain.Abstractions.IDomainEventDispatcher"/> only AFTER the transaction commits, so a broker
/// publish can never precede (or outlive) the DB write — no ghost events on rollback.
/// Handlers must record events on <see cref="Domain.DomainEventCollector"/> (aggregates raised
/// via <c>AddDomainEvent</c> are collected from handler-owned instances by convention
/// documented in each handler); dispatch here is the single chokepoint.
/// </para>
/// </summary>
public sealed class TransactionBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDomainEventDispatcher _eventDispatcher;
    private readonly ILogger<TransactionBehavior<TRequest, TResponse>> _logger;

    public TransactionBehavior(
        IUnitOfWork unitOfWork,
        IDomainEventDispatcher eventDispatcher,
        ILogger<TransactionBehavior<TRequest, TResponse>> logger)
    {
        _unitOfWork = unitOfWork;
        _eventDispatcher = eventDispatcher;
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

            // Post-commit dispatch: broker publishes happen outside the transaction.
            var pending = DomainEventCollector.Drain();
            foreach (var evt in pending)
                await _eventDispatcher.PublishAsync(evt, cancellationToken).ConfigureAwait(false);

            return response;
        }
        catch
        {
            _logger.LogWarning("[TransactionBehavior] Rolling back transaction for {Request}", typeof(TRequest).Name);
            await _unitOfWork.RollbackAsync(cancellationToken).ConfigureAwait(false);
            DomainEventCollector.Clear();
            throw;
        }
    }
}
