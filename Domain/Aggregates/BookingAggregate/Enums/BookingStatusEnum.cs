namespace Domain.Aggregates.BookingAggregate.Enums
{
    public enum BookingStatusEnum
    {
        Pending = 0,
        PendingPayment = 5,
        Confirmed = 1,
        Cancelled = 2,
        Expired = 3,
        Refunded = 4,
    }
}


