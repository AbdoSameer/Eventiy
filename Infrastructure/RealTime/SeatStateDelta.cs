using System.Text.Json.Serialization;

namespace Infrastructure.RealTime;

public sealed record SeatStateDelta(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("seatId")] string SeatId,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("ts")] long Ts
)
{
    public static SeatStateDelta Delta(string seatId, string status, long ts) =>
        new("DELTA", seatId, status, ts);

    public static SeatStateDelta Collision(string seatId, string reason, long ts) =>
        new("COLLISION", seatId, reason, ts);
}
