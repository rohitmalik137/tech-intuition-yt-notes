namespace TicketBooking;

public enum SeatType { Regular, Premium, Recliner }
public enum SeatStatus { Available, Held, Booked }

// A Seat is just "which seat is this": immutable identity data, so it is a record and needs no lock.
public sealed record Seat(string Id, SeatType Type);

// ════════════════════════════════════════════════════════════════════════════════════════════════
// THE STATE PATTERN, in plain English
//
//   Imagine a seat as a traffic light. Each colour (state) knows which changes are allowed:
//     Green (Available) → can become Held.
//     Amber (Held)      → can become Booked or go back to Available.
//     Red   (Booked)    → stays Red.
//   Instead of one big `if (status == Held && ...)` tangle, each state is a tiny class that answers
//   "what happens if I'm asked to Hold / Confirm / Release?". An illegal request returns `null`.
//
//   WHY BOTHER?  Adding a new state (e.g. "Blocked for maintenance") = adding ONE class,
//   not editing every `if` in the program.
// ════════════════════════════════════════════════════════════════════════════════════════════════
public interface ISeatState
{
    SeatStatus Status { get; }

    // Each method returns the NEXT state, or null (= "that move is not allowed from here").
    // The `?` after ISeatState means "this reference may be null".
    ISeatState? Hold(string holdId, DateTime expiresAt);
    ISeatState? Confirm(string holdId);
    ISeatState? Release(string holdId);

    /// <summary>
    /// LAZY EXPIRY: nobody runs a timer. Whenever we look at a seat we first call Tick(now).
    /// A Held seat whose time has run out turns itself back into Available. No background thread,
    /// nothing to race with, and trivial to test with a fake clock.
    /// </summary>
    // A "default interface method": implementations may override it, otherwise "do nothing" (return this).
    ISeatState Tick(DateTime now) => this;
}

internal sealed class AvailableState : ISeatState
{
    // Static instance: the Available state carries no data, so one shared object is enough.
    public static readonly AvailableState Instance = new();
    public SeatStatus Status => SeatStatus.Available;

    public ISeatState? Hold(string holdId, DateTime expiresAt) => new HeldState(holdId, expiresAt);  // legal
    public ISeatState? Confirm(string holdId) => null;   // nobody holds it, so there is nothing to confirm
    public ISeatState? Release(string holdId) => null;   // nothing to release
}

internal sealed class HeldState : ISeatState
{
    // Held DOES carry data: who holds it (holdId) and until when.
    private readonly string _holdId;
    private readonly DateTime _expiresAt;
    public HeldState(string holdId, DateTime expiresAt) { _holdId = holdId; _expiresAt = expiresAt; }

    public SeatStatus Status => SeatStatus.Held;

    public ISeatState? Hold(string holdId, DateTime expiresAt) => null;   // already held → the loser of the race lands here

    // The key line of the whole design: "confirm ONLY IF the hold is still mine".
    // `cond ? a : b` is the ternary operator: if the ids match give Booked, else null (refuse).
    public ISeatState? Confirm(string holdId) => holdId == _holdId ? BookedState.Instance : null;
    public ISeatState? Release(string holdId) => holdId == _holdId ? AvailableState.Instance : null;

    // Timer ran out? Behave as Available. (>= so "expires exactly now" counts as expired.)
    public ISeatState Tick(DateTime now) => now >= _expiresAt ? AvailableState.Instance : this;
}

internal sealed class BookedState : ISeatState
{
    public static readonly BookedState Instance = new();
    public SeatStatus Status => SeatStatus.Booked;
    public ISeatState? Hold(string holdId, DateTime expiresAt) => null;
    public ISeatState? Confirm(string holdId) => null;
    public ISeatState? Release(string holdId) => null;   // Booked is terminal (cancellation is an extension)
}

/// <summary>
/// One seat in one show. It owns TWO things: its current state and ITS OWN lock.
///
/// WHY one lock per seat? Two fans booking different seats should never wait for each other.
/// A single lock for the whole show would be correct but would make every booking queue up.
/// </summary>
public sealed class ShowSeat
{
    // ┌─ WHAT IS A LOCK? (plain English) ───────────────────────────────────────────────────────────┐
    // │ Think of a bathroom with ONE key hanging on a hook. Whoever takes the key goes in; everyone    │
    // │ else waits outside until the key is back. `lock (obj) { ... }` means "take the key for obj,    │
    // │ run this block, put the key back (even if an error happens)".                                  │
    // │ Any object can serve as the "key"; we use a private object so no outside code can grab it.     │
    // └────────────────────────────────────────────────────────────────────────────────────────────────┘
    private readonly object _gate = new();
    private ISeatState _state = AvailableState.Instance;

    public ShowSeat(Seat seat) => Seat = seat;
    public Seat Seat { get; }

    // `internal` = visible inside this project only. Show needs the key to lock several seats at once.
    internal object Gate => _gate;

    public SeatStatus StatusAt(DateTime now)
    {
        // Reading AND updating (Tick may change the state) must be one uninterrupted step, so we lock.
        lock (_gate) { _state = _state.Tick(now); return _state.Status; }
    }

    // ─ The "Unsafe" suffix is a WARNING label: these methods do NOT lock. The caller (Show) must
    //   already be holding this seat's key. This lets Show lock many seats first, then act.
    //
    // `Func<ISeatState, ISeatState?> transition` is "a function passed in as a parameter":
    // it takes the current state and returns the next one (or null if illegal). It lets Show say
    // WHICH move to try (hold / confirm / release) without duplicating the locking code.
    internal ISeatState? PlanUnsafe(Func<ISeatState, ISeatState?> transition, DateTime now)
    {
        _state = _state.Tick(now);        // expire first
        return transition(_state);        // ask the state "is this legal?"; DON'T apply yet
    }
    internal void SetUnsafe(ISeatState next) => _state = next;   // apply the decision
}
