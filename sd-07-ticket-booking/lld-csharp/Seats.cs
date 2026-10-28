namespace TicketBooking;

public enum SeatType { Regular, Premium, Recliner }
public enum SeatStatus { Available, Held, Booked }

public sealed record Seat(string Id, SeatType Type);

// ── State pattern: a seat's life is Available → Held → Booked (or back to Available) ──────────
// Each state decides which transitions are legal. A transition returns the NEXT state, or null if illegal.
public interface ISeatState
{
    SeatStatus Status { get; }
    ISeatState? Hold(string holdId, DateTime expiresAt);
    ISeatState? Confirm(string holdId);
    ISeatState? Release(string holdId);
    /// <summary>Lazy expiry: a Held state whose timer has run out behaves as Available. No background thread needed.</summary>
    ISeatState Tick(DateTime now) => this;
}

internal sealed class AvailableState : ISeatState
{
    public static readonly AvailableState Instance = new();
    public SeatStatus Status => SeatStatus.Available;
    public ISeatState? Hold(string holdId, DateTime expiresAt) => new HeldState(holdId, expiresAt);
    public ISeatState? Confirm(string holdId) => null;   // cannot confirm what nobody holds
    public ISeatState? Release(string holdId) => null;
}

internal sealed class HeldState : ISeatState
{
    private readonly string _holdId;
    private readonly DateTime _expiresAt;
    public HeldState(string holdId, DateTime expiresAt) { _holdId = holdId; _expiresAt = expiresAt; }
    public SeatStatus Status => SeatStatus.Held;
    public ISeatState? Hold(string holdId, DateTime expiresAt) => null;                        // already held
    public ISeatState? Confirm(string holdId) => holdId == _holdId ? BookedState.Instance : null;   // "only if it's still mine"
    public ISeatState? Release(string holdId) => holdId == _holdId ? AvailableState.Instance : null;
    public ISeatState Tick(DateTime now) => now >= _expiresAt ? AvailableState.Instance : this;
}

internal sealed class BookedState : ISeatState
{
    public static readonly BookedState Instance = new();
    public SeatStatus Status => SeatStatus.Booked;
    public ISeatState? Hold(string holdId, DateTime expiresAt) => null;
    public ISeatState? Confirm(string holdId) => null;
    public ISeatState? Release(string holdId) => null;   // a booked seat is terminal (cancellation is an extension)
}

/// <summary>One seat in one show. It owns its own lock, so seats never contend with each other.</summary>
public sealed class ShowSeat
{
    private readonly object _gate = new();
    private ISeatState _state = AvailableState.Instance;

    public ShowSeat(Seat seat) => Seat = seat;
    public Seat Seat { get; }
    internal object Gate => _gate;

    public SeatStatus StatusAt(DateTime now)
    {
        lock (_gate) { _state = _state.Tick(now); return _state.Status; }
    }

    // The *Unsafe members must be called while holding Gate (Show takes the locks in a fixed order).
    internal ISeatState? PlanUnsafe(Func<ISeatState, ISeatState?> transition, DateTime now)
    {
        _state = _state.Tick(now);
        return transition(_state);
    }
    internal void SetUnsafe(ISeatState next) => _state = next;
}
