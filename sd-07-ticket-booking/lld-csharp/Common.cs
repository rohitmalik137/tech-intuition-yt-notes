namespace TicketBooking;

// ┌─ SYNTAX NOTE: `record` ────────────────────────────────────────────────────────────────────────┐
// │ A record is a class whose job is "just carry data". The compiler writes Equals/ToString for you  │
// │ and the properties are read-only by default. Two records with the same values are "equal".       │
// └──────────────────────────────────────────────────────────────────────────────────────────────────┘
//
// ┌─ SYNTAX NOTE: generics `<T>` ──────────────────────────────────────────────────────────────────┐
// │ Result<T> is a template: Result<Hold> carries a Hold, Result<Booking> carries a Booking.         │
// │ `T? Value` means "a T, or nothing (null) if this is a failure".                                  │
// └──────────────────────────────────────────────────────────────────────────────────────────────────┘

/// <summary>
/// Instead of throwing exceptions for expected problems ("seat just taken"), we RETURN a Result.
/// Callers must check <c>Ok</c> before using <c>Value</c>. Exceptions stay for genuine bugs.
/// </summary>
public sealed record Result<T>(bool Ok, T? Value, string? Error)
{
    // `static` factory methods give readable call sites: Result<Hold>.Success(hold) / .Fail("why").
    public static Result<T> Success(T value) => new(true, value, null);
    public static Result<T> Fail(string error) => new(false, default, error);   // default = null for reference types
}

/// <summary>
/// WHY an interface for the clock? Tests can substitute a FakeClock and "jump" 8 minutes in a
/// microsecond instead of calling Thread.Sleep. Anything that touches the outside world
/// (time, network, disk) should sit behind an interface like this.  (SOLID: Dependency Inversion.)
/// </summary>
public interface IClock { DateTime UtcNow { get; } }

/// <summary>The real clock, used in production.</summary>
public sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;      // `=>` after a property = "this is how you compute it"
}

/// <summary>A clock we can move by hand, used in tests.</summary>
// `FakeClock(DateTime start)` is a C# 12 "primary constructor": `start` is available inside the class body.
public sealed class FakeClock(DateTime start) : IClock
{
    private DateTime _now = start;
    public DateTime UtcNow => _now;
    public void Advance(TimeSpan by) => _now += by;   // "move time forward"; nothing else in the app changes
}
