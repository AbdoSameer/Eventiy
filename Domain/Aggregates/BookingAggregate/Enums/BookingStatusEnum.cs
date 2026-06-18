using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Aggregates.OrderAggregate.Enums
{
    public enum BookingStatusEnum
    {
        // BookingStatus Enum
        // For booking tickets status 

        Unknown = 0,
        Confirmed = 1,
        Cancelled = 2,
        Expired = 3,
        Refunded = 4,
        RefundCancelled = 5,
        RefundExpired = 6,
    }

}
