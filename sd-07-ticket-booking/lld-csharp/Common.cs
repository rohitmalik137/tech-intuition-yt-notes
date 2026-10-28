namespace TicketBooking;

/// <summary>Tiny Result type: callers must look at Ok before touching Value.</summary>
public sealed record Result<T>(bool Ok, T? Value, string? Error)
{
    public static Result<T> Success(T value) => new(true, value, null);
    public static Result<T> Fail(string error) => new(false, default, error);
}

/// <summary>Time is a dependency, so hold expiry is testable without Thread.Sleep.</summary>
public interface IClock { DateTime UtcNow { get; } }

public sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}

public sealed class FakeClock(DateTime start) : IClock
{
    private DateTime _now = start;
    public DateTime UtcNow => _now;
    public void Advance(TimeSpan by) => _now += by;
}
