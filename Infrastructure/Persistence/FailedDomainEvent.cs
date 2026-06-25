using Domain.Common;

namespace Infrastructure.Persistence
{
    public partial class UnitOfWork
    {
        private class FailedDomainEvent
        {
            public IDomainEvent Event { get; set; }
            public string Error { get; set; }
            public DateTime FailedAt { get; set; }
            public int RetryCount { get; set; }
        }
    }
}