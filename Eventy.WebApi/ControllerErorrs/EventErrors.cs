
namespace Eventy.WebApi.ControllerErorrs
{
    public static class EventErrors
    {

        public static string EventIdNotMatch()
            => "The event ID in the URL does not match the event ID in the request body.";
    }
}
