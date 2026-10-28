namespace TicketBooking;

public sealed class Show
{
    // After the constructor finishes this dictionary is NEVER modified, so many threads may read it
    // at the same time without a lock. (Immutable data is the easiest kind of thread-safe data.)
    private readonly Dictionary<string, ShowSeat> _seats;

    public Show(string id, string movie, DateTime startsAtUtc, IEnumerable<Seat> seats)
    {
        Id = id; Movie = movie; StartsAtUtc = startsAtUtc;
        // LINQ one-liner: turn the list of seats into a dictionary  seatId → ShowSeat.
        // `s => s.Id` is a lambda: "given a seat s, the key is s.Id".
        _seats = seats.ToDictionary(s => s.Id, s => new ShowSeat(s), StringComparer.Ordinal);
    }

    public string Id { get; }
    public string Movie { get; }
    public DateTime StartsAtUtc { get; }

    public Seat GetSeat(string seatId) => Find(seatId).Seat;
    public SeatStatus StatusOf(string seatId, DateTime now) => Find(seatId).StatusAt(now);

    private ShowSeat Find(string seatId) =>
        _seats.TryGetValue(seatId, out var s) ? s : throw new KeyNotFoundException($"Unknown seat {seatId} for show {Id}");
    // ^ `out var s`: TryGetValue returns true/false AND hands the found value back through `s`.

    // ── The three public operations. They all share ONE routine (Transition) and differ only in the move. ──

    /// <summary>All-or-nothing hold. Either every seat becomes Held, or none does.</summary>
    public bool TryHoldAll(IEnumerable<string> seatIds, string holdId, DateTime now, TimeSpan ttl) =>
        // `s => s.Hold(...)` is a lambda: "for each seat's current state s, try to Hold it".
        Transition(seatIds, now, s => s.Hold(holdId, now + ttl), allOrNothing: true);

    public bool TryConfirmAll(IEnumerable<string> seatIds, string holdId, DateTime now) =>
        Transition(seatIds, now, s => s.Confirm(holdId), allOrNothing: true);

    /// <summary>Best effort and idempotent: seats already expired or not ours are simply skipped.</summary>
    public void ReleaseAll(IEnumerable<string> seatIds, string holdId, DateTime now) =>
        Transition(seatIds, now, s => s.Release(holdId), allOrNothing: false);

    // ════════════════════════════════════════════════════════════════════════════════════════════
    // THE HEART OF THE DESIGN
    //
    //   Step 1  Sort the seat ids alphabetically.
    //   Step 2  Take the lock (key) of each seat, IN THAT ORDER, and keep them all.
    //   Step 3  PLAN: ask every seat "would this move be legal?"  (nothing changes yet)
    //   Step 4  If any seat says no (and we need all-or-nothing) → stop; nothing was changed.
    //   Step 5  Otherwise APPLY the move to every seat.
    //   Step 6  Put all keys back.
    //
    //   WHY SORTED?  (the deadlock story)  Suppose fan A wants seats B3 then B4, and fan B wants
    //   B4 then B3. A grabs B3's key, B grabs B4's key, then each waits forever for the other's key.
    //   That is a DEADLOCK. If everyone takes keys in the same alphabetical order, the cycle
    //   cannot form: both would try B3 first, and one simply waits its turn.
    //
    //   WHY PLAN-THEN-APPLY?  Because we hold all the keys, nobody can change a seat between the
    //   "can we?" check and the "do it" step. And since we only apply if ALL checks passed, there is
    //   never a half-done state to undo.
    // ════════════════════════════════════════════════════════════════════════════════════════════
    private bool Transition(IEnumerable<string> seatIds, DateTime now, Func<ISeatState, ISeatState?> step, bool allOrNothing)
    {
        var ordered = seatIds
            .Distinct()                                   // the same seat listed twice must not be locked twice
            .OrderBy(id => id, StringComparer.Ordinal)    // Step 1: the global lock order
            .Select(Find)                                 // id → ShowSeat (throws if the id doesn't exist)
            .ToList();

        // We pass the body of Steps 3–5 as a lambda `() => { ... }` ("a note of instructions") to
        // WithAllLocked, which holds every key while that note is carried out.
        return WithAllLocked(ordered, 0, () =>
        {
            var plans = ordered.Select(s => s.PlanUnsafe(step, now)).ToList();        // Step 3: decide
            if (allOrNothing && plans.Any(p => p is null)) return false;              // Step 4: one "no" vetoes all
            for (var i = 0; i < ordered.Count; i++)                                   // Step 5: apply
                if (plans[i] is { } next) ordered[i].SetUnsafe(next);                 //   `is { } next` = "if not null, call it next"
            return true;
        });
    }

    // ┌─ RECURSION, slowly ─────────────────────────────────────────────────────────────────────────┐
    // │ We need to hold N locks at once, but `lock` only takes one object. So we nest them by calling   │
    // │ ourselves: lock seat[0] { lock seat[1] { lock seat[2] { ...run the action... } } }             │
    // │ `i` is "which seat's lock are we about to take?". When i == Count, all keys are held → run it.  │
    // │ When the action finishes, each `lock` block ends and releases its key, in reverse order.       │
    // └──────────────────────────────────────────────────────────────────────────────────────────────────┘
    private static bool WithAllLocked(IReadOnlyList<ShowSeat> seats, int i, Func<bool> action)
    {
        if (i == seats.Count) return action();                    // base case: every key is in our hand
        lock (seats[i].Gate) return WithAllLocked(seats, i + 1, action);   // take this key, then deal with the rest
    }
}
