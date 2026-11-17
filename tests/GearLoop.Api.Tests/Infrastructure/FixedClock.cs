using GearLoop.Api.Services;

namespace GearLoop.Api.Tests.Infrastructure;

public class FixedClock : IClock
{
    public FixedClock(DateTime utcNow)
    {
        UtcNow = utcNow;
        Today = DateOnly.FromDateTime(utcNow);
    }

    public DateTime UtcNow { get; }
    public DateOnly Today { get; }
}
