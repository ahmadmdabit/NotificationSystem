using MediatR;

using Microsoft.Extensions.Logging;

using Shared.Application.Abstractions;
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
    private readonly IUnitOfWork unitOfWork;
    private readonly IDomainEventDispatcher eventDispatcher;
    private readonly ILogger<TransactionBehavior<TRequest, TResponse>> logger;

    public TransactionBehavior(
        IUnitOfWork unitOfWork,
        IDomainEventDispatcher eventDispatcher,
        ILogger<TransactionBehavior<TRequest, TResponse>> logger)
    {
        this.unitOfWork = unitOfWork;
        this.eventDispatcher = eventDispatcher;
        this.logger = logger;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        // Only wrap commands (ICommand) in transactions
        if (request is not ICommand)
            return await next().ConfigureAwait(false);

        logger.LogInformation("[TransactionBehavior] Beginning transaction for {Request}", typeof(TRequest).Name);
        await unitOfWork.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        // Seed the collector HERE, in the pipeline's own execution context. AsyncLocal
        // mutations do not flow back out of an awaited callee, so without this the
        // handler's DomainEventCollector.Add() allocates a list this pipeline can never
        // see, and the post-commit Drain() below returns empty -- silently dropping every
        // domain event. See DomainEventCollector.Seed for the full explanation.
        DomainEventCollector.Seed();

        try
        {
            var response = await next().ConfigureAwait(false);
            await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);
            logger.LogInformation("[TransactionBehavior] Transaction committed for {Request}", typeof(TRequest).Name);

            // Post-commit dispatch: broker publishes happen outside the transaction.
            var pending = DomainEventCollector.Drain();
            foreach (var evt in pending)
                await eventDispatcher.PublishAsync(evt, cancellationToken).ConfigureAwait(false);

            return response;
        }
        catch
        {
            logger.LogWarning("[TransactionBehavior] Rolling back transaction for {Request}", typeof(TRequest).Name);
            await unitOfWork.RollbackAsync(cancellationToken).ConfigureAwait(false);
            DomainEventCollector.Clear();
            throw;
        }
    }
}
