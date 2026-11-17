using System.Text.Json;
using System.Text.Json.Serialization;

namespace GearLoop.Api.Tests.Infrastructure;

public static class TestJson
{
    // Mirrors the Api's JsonStringEnumConverter so enum DTO fields (ReservationStatus, etc.)
    // round-trip correctly in HttpClient calls against the test host.
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };
}
