namespace GearLoop.Api.Services;

/// Thin abstraction over the system clock so reservation-window rules are deterministic in tests.
public interface IClock
{
    DateTime UtcNow { get; }
    DateOnly Today { get; }
}

public class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
    public DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);
}
