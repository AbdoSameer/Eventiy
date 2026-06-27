using Domain.Common;
using Domain.Primitives;

namespace Application.Abstractions.Persistence
{
    public interface IEventValidator<in TEvent>
        where TEvent : IDomainEvent
    {
        Task<Result> ValidateAsync(TEvent @event);
    }
}
