using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Aggregates.EventAggregate.Enums
{
    public static class EventStatus
    {
        public static string Draft => "Draft";
        public static string Published => "Published";
        public static string Cancelled => "Cancelled";
        public static string Completed => "Completed";

        enum Status
        {
            Draft,
            Published,
            Cancelled,
            Completed
        }

    }
}
