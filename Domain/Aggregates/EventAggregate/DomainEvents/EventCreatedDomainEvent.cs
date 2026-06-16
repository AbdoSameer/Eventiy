using Domain.Aggregates.EventAggregate.ValueObject;
using Domain.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Aggregates.EventAggregate.DomainEvents
{
    public sealed record EventCreatedDomainEvent(EventId EventId) : IDomainEvent
    {
        public string Name => throw new NotImplementedException();

        public string Domain => throw new NotImplementedException();
    }
}
