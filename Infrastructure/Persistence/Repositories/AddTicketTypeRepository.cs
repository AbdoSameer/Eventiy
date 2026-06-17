//using Application.Abstractions.Persistence;
//using Domain.Aggregates.EventAggregate;
//using Microsoft.EntityFrameworkCore.Storage;

//namespace Infrastructure.Persistence.Repositories
//{
//    internal class AddTicketTypeRepository : IAddTicketTypeRepository
//    {
//        private readonly ApplicationDbContext _applicationDbContext;

//        public AddTicketTypeRepository(ApplicationDbContext applicationDbContext)
//        {
//            _applicationDbContext = applicationDbContext;
//        }
//        public Task<TicketType> AddTicketTypeAsync(TicketType ticketType, CancellationToken cancellationToken)
//        {
//            var entityEntry = _applicationDbContext.TicketTypes.Add(ticketType);
            
//            return Task.FromResult(entityEntry.Entity);

//        }
//    }
//}
