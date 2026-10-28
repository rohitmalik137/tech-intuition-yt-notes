using System.Collections.Concurrent;

namespace TicketBooking;

/// <summary>
/// The FACADE: the one friendly front door the rest of the app uses (hold / confirm / release).
/// It COORDINATES the other classes but owns no seat rules itself: those live in Show and ShowSeat.
/// </summary>
public sealed class BookingService
{
    public const int MaxSeatsPerHold = 6;   // cap so nobody can hoard a whole section

    private readonly IClock _clock;
    private readonly IPaymentGateway _payments;
    private readonly IBookingRepository _bookings;
    private readonly IPricingStrategy _pricing;
    private readonly IReadOnlyList<IBookingListener> _listeners;
    private readonly TimeSpan _holdTtl;

    // ConcurrentDictionary = a dictionary that is safe to use from many threads at once without our own lock.
    private readonly ConcurrentDictionary<string, Show> _shows = new();
    private readonly ConcurrentDictionary<string, Hold> _holds = new();

    // ┌─ Lazy<T> + GetOrAdd: "run this exactly once per key" ────────────────────────────────────────┐
    // │ Problem: two threads send the same idempotency key at the same moment. ConcurrentDictionary's    │
    // │ GetOrAdd is thread-safe, but it may run its "create the value" function TWICE under a race.      │
    // │ Fix: don't store the answer, store a Lazy (a recipe that is only cooked the first time anyone    │
    // │ calls .Value; later callers get the same cooked result). So the confirm logic runs ONCE per key. │
    // └──────────────────────────────────────────────────────────────────────────────────────────────────┘
    private readonly ConcurrentDictionary<string, Lazy<Result<Booking>>> _confirmations = new();

    public BookingService(IClock clock, IPaymentGateway payments, IBookingRepository bookings,
        IPricingStrategy pricing, IEnumerable<IBookingListener> listeners, TimeSpan holdTtl)
    {
        _clock = clock; _payments = payments; _bookings = bookings; _pricing = pricing;
        _listeners = listeners.ToList(); _holdTtl = holdTtl;
    }

    public void AddShow(Show show) => _shows[show.Id] = show;

    // ── 1. HOLD ───────────────────────────────────────────────────────────────────────────────────
    // "Reserve these seats for 8 minutes." In the distributed design this is Redis SET NX EX;
    // here Show.TryHoldAll does the same job with in-process locks.
    public Result<Hold> CreateHold(string userId, string showId, IReadOnlyCollection<string> seatIds)
    {
        if (!_shows.TryGetValue(showId, out var show)) return Result<Hold>.Fail("unknown show");
        if (seatIds.Count == 0 || seatIds.Count > MaxSeatsPerHold) return Result<Hold>.Fail($"choose 1 to {MaxSeatsPerHold} seats");

        var now = _clock.UtcNow;
        var holdId = Guid.NewGuid().ToString("N");   // a unique id; the hold on each seat remembers it ("only if it's still mine")
        try
        {
            if (!show.TryHoldAll(seatIds, holdId, now, _holdTtl)) return Result<Hold>.Fail("one or more seats were just taken");
        }
        catch (KeyNotFoundException e) { return Result<Hold>.Fail(e.Message); }   // someone sent a seat id that doesn't exist

        var hold = new Hold(holdId, userId, showId, seatIds.Distinct().OrderBy(s => s, StringComparer.Ordinal).ToList(), now + _holdTtl);
        _holds[holdId] = hold;
        return Result<Hold>.Success(hold);
    }

    // ── 2. CONFIRM ────────────────────────────────────────────────────────────────────────────────
    // Wraps the real work (ConfirmCore) so the same idempotency key can never run it twice.
    public Result<Booking> Confirm(string holdId, string idempotencyKey) =>
        _confirmations.GetOrAdd(idempotencyKey, _ => new Lazy<Result<Booking>>(() => ConfirmCore(holdId, idempotencyKey))).Value;
    //                                              ^ `_ =>` = a lambda that ignores its argument

    // The same TWO GUARDS as the HLD note:
    //   guard 1: is the hold still mine and unexpired?   guard 2: do the seats confirm, and does UNIQUE agree?
    // Any failure AFTER money moved must refund, otherwise the customer pays for nothing.
    private Result<Booking> ConfirmCore(string holdId, string idempotencyKey)
    {
        if (!_holds.TryGetValue(holdId, out var hold)) return Result<Booking>.Fail("hold not found");
        var show = _shows[hold.ShowId];
        var now = _clock.UtcNow;

        // Cheap early exit BEFORE touching money: if the timer already ran out, don't even charge.
        if (now >= hold.ExpiresAt) return Result<Booking>.Fail("hold expired");

        var amount = hold.SeatIds.Sum(id => _pricing.PriceFor(show, show.GetSeat(id)));   // LINQ Sum over the seats' prices

        var pay = _payments.Charge(idempotencyKey, amount);
        if (!pay.Success)
        {
            // Card declined: free the seats immediately so other fans can have them (don't wait 8 minutes).
            show.ReleaseAll(hold.SeatIds, holdId, _clock.UtcNow);
            _holds.TryRemove(holdId, out _);                    // `out _` = "I don't care about the removed value"
            return Result<Booking>.Fail(pay.Error ?? "payment failed");   // `??` = "if left side is null, use the right"
        }

        // THE RACE FROM THE VIDEO: the money moved, but did the hold survive the time spent paying?
        // Re-ask the seats: "confirm ONLY IF the hold is still mine". If not, we refund automatically.
        if (!show.TryConfirmAll(hold.SeatIds, holdId, _clock.UtcNow))
        {
            _payments.Refund(pay.PaymentId);
            return Result<Booking>.Fail("hold expired while paying; payment refunded");
        }

        var booking = new Booking(Guid.NewGuid().ToString("N"), hold.ShowId, hold.SeatIds, hold.UserId, pay.PaymentId, amount, _clock.UtcNow);

        // The UNIQUE safety net. If everything above is correct this can never fail; it exists so that
        // ONE bug anywhere above still can't produce a double sale.
        if (!_bookings.TryAdd(booking))
        {
            _payments.Refund(pay.PaymentId);
            return Result<Booking>.Fail("seat already sold; payment refunded");
        }

        _holds.TryRemove(holdId, out _);
        foreach (var l in _listeners)
        {
            try { l.OnConfirmed(booking); }
            catch { /* a broken email service must NEVER undo a sale. (Production: outbox + retry.) */ }
        }
        return Result<Booking>.Success(booking);
    }

    // ── 3. RELEASE ────────────────────────────────────────────────────────────────────────────────
    public bool Release(string holdId, string userId)
    {
        if (!_holds.TryGetValue(holdId, out var hold) || hold.UserId != userId) return false;   // only the owner may release
        _shows[hold.ShowId].ReleaseAll(hold.SeatIds, holdId, _clock.UtcNow);
        return _holds.TryRemove(holdId, out _);
    }
}
