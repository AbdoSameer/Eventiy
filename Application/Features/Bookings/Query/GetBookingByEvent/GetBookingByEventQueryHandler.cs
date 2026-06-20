using Application.Abstractions.Messaging;
using Application.Abstractions.Persistence;
using Domain.Aggregates.BookingAggregate;
using Domain.Aggregates.EventAggregate.ValueObject;
using Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Bookings.Query.GetBookingByEvent
{
    public class GetBookingByEventQueryHandler
        : IQueryHandler<GetBookingByEventQuery, List<GetBookingByEventQueryResponse>>
    {
        private readonly IApplicationReadDbContext _context;

        public GetBookingByEventQueryHandler(IApplicationReadDbContext context)
        {
            _context = context;
        }

        public async Task<Result<List<GetBookingByEventQueryResponse>>> Handle(GetBookingByEventQuery request, CancellationToken cancellationToken)
        {
            var EventIdResult = EventId.Create(request.EventId);
            if (EventIdResult.IsFailure)
            {
                return Result<List<GetBookingByEventQueryResponse>>
                    .Failure(EventIdResult.Error);
            }


            var bookings = await _context.Query<Booking>()
                .Where(b => b.EventId == EventIdResult.Value)
                .Select(b => new GetBookingByEventQueryResponse
                (
                    b.Id.Value,
                    b.EventId.Value,
                    b.UserId.Value,
                    b.BookingDate,
                    b.Quantity,
                    b.TotalAmount
                )).ToListAsync(cancellationToken);

            if (!bookings.Any())
            {
                return Result<List<GetBookingByEventQueryResponse>>
                    .Failure("No bookings found for the specified event.");
            }

            return Result<List<GetBookingByEventQueryResponse>>.Success(bookings);
        }
    }
}

