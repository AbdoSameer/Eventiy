using Application.Abstractions.Persistence;
using Application.Abstractions.Outbox;
using Domain.Common;
using MediatR;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace Infrastructure.Persistence
{
    public partial class UnitOfWork : IUnitOfWork
    {
        private readonly ApplicationDbContext _context;
        private readonly IMediator _mediator;
        private readonly ILogger<UnitOfWork> _logger;
        private readonly IOutboxMessageService _outboxService;

        public UnitOfWork(
            IMediator mediator,
            ApplicationDbContext context,
            IOutboxMessageService outboxService,
            ILogger<UnitOfWork> logger = null)
        {
            _mediator = mediator;
            _context = context;
            _outboxService = outboxService;
            _logger = logger;
        }

        public async Task<int> CommitAsync(CancellationToken cancellationToken = default)
        {
            var domainEvents = ExtractDomainEvents();

            if (domainEvents.Any())
            {
                await _outboxService.AddFromDomainEventsAsync(domainEvents, cancellationToken);
            }

            var result = await _context.SaveChangesAsync(cancellationToken);

            await PublishDomainEventsSafelyAsync(domainEvents, cancellationToken);

            return result;
        }

        private List<IDomainEvent> ExtractDomainEvents()
        {
            var domainEvents = new List<IDomainEvent>();

            var aggregates = _context.ChangeTracker.Entries()
                .Where(x => x.Entity is IAggregateRoot)
                .Select(x => (IAggregateRoot)x.Entity)
                .ToList();

            foreach (var aggregate in aggregates)
            {
                var events = aggregate.DomainEvents?
                                      .ToList() ?? new List<IDomainEvent>();
                
                domainEvents.AddRange(events);

                aggregate.ClearDomainEvents();
            }

            return domainEvents;
        }

        private async Task PublishDomainEventsSafelyAsync(
            List<IDomainEvent> domainEvents,
            CancellationToken cancellationToken)
        {
            if (domainEvents == null || !domainEvents.Any())
                return;

            var publishTasks = domainEvents.Select(async domainEvent =>
            {
                try
                {
                    await _mediator.Publish(domainEvent, cancellationToken);

                    _logger?.LogInformation(
                        "Domain event published successfully: {EventType} for {EventId}",
                        domainEvent.GetType().Name,
                        domainEvent.GetHashCode());
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex,
                        "Failed to publish domain event: {EventType}. Error: {ErrorMessage}",
                        domainEvent.GetType().Name,
                        ex.Message);

                    await AddToRetryQueueAsync(domainEvent, ex);
                }
            });

            await Task.WhenAll(publishTasks);
        }

        private readonly ConcurrentQueue<FailedDomainEvent> _retryQueue = new();

        private async Task AddToRetryQueueAsync(IDomainEvent domainEvent, Exception exception)
        {
            var failedEvent = new FailedDomainEvent
            {
                Event = domainEvent,
                Error = exception.Message,
                FailedAt = DateTime.UtcNow,
                RetryCount = 0
            };

            _retryQueue.Enqueue(failedEvent);

            if (_retryQueue.Count % 10 == 0)
            {
                await ProcessRetryQueueAsync();
            }
        }

        private async Task ProcessRetryQueueAsync()
        {
            var maxRetries = 3;
            var processed = new List<FailedDomainEvent>();

            while (_retryQueue.TryDequeue(out var failedEvent))
            {
                try
                {
                    if (failedEvent.RetryCount >= maxRetries)
                    {
                        _logger?.LogError(
                            "Max retries reached for event {EventType}. Giving up.",
                            failedEvent.Event.GetType().Name);
                        continue;
                    }

                    failedEvent.RetryCount++;

                    var delay = TimeSpan.FromSeconds(Math.Pow(2, failedEvent.RetryCount));
                    await Task.Delay(delay);

                    await _mediator.Publish(failedEvent.Event);

                    _logger?.LogInformation(
                        "Retry {RetryCount} succeeded for event {EventType}",
                        failedEvent.RetryCount,
                        failedEvent.Event.GetType().Name);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex,
                        "Retry {RetryCount} failed for event {EventType}",
                        failedEvent.RetryCount,
                        failedEvent.Event.GetType().Name);

                    _retryQueue.Enqueue(failedEvent);
                }
            }
        }

        // ✅ Helper method for OutboxProcessor to save without triggering events again
        public async Task<int> CommitWithoutEventsAsync(CancellationToken cancellationToken = default)
        {
            return await _context.SaveChangesAsync(cancellationToken);
        }
    }

    // Helper class for retry queue
    internal class FailedDomainEvent
    {
        public IDomainEvent Event { get; set; }
        public string Error { get; set; }
        public DateTime FailedAt { get; set; }
        public int RetryCount { get; set; }
    }
}