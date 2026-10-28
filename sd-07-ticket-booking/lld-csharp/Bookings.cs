namespace TicketBooking;

// A Hold is "this user is temporarily reserving these seats until this time". Immutable data → record.
public sealed record Hold(string HoldId, string UserId, string ShowId, IReadOnlyList<string> SeatIds, DateTime ExpiresAt);

// A Booking is the permanent sale.
public sealed record Booking(
    string BookingId, string ShowId, IReadOnlyList<string> SeatIds, string UserId,
    string PaymentId, decimal Amount, DateTime CreatedAt);

public interface IBookingRepository
{
    /// <summary>
    /// The LAST LINE OF DEFENCE. In production this is a database with
    ///   UNIQUE (show_id, seat_id)
    /// which refuses a second sale of the same seat even if every other check had a bug.
    /// Returns false if ANY of the booking's seats is already sold.
    /// </summary>
    bool TryAdd(Booking booking);
}

public sealed class InMemoryBookingRepository : IBookingRepository
{
    private readonly object _gate = new();
    // A HashSet of (show, seat) pairs = "which seats are already sold". Checking membership is instant.
    // `(string Show, string Seat)` is a TUPLE: two values bundled together without writing a class.
    private readonly HashSet<(string Show, string Seat)> _sold = new();
    private readonly List<Booking> _all = new();

    // Return a COPY (ToList) so callers can't modify our internal list while another thread is writing.
    public IReadOnlyList<Booking> All { get { lock (_gate) return _all.ToList(); } }

    public bool TryAdd(Booking booking)
    {
        lock (_gate)   // check + insert must be ONE step, or two threads could both pass the check
        {
            if (booking.SeatIds.Any(s => _sold.Contains((booking.ShowId, s)))) return false;   // ANY seat sold → refuse all
            foreach (var s in booking.SeatIds) _sold.Add((booking.ShowId, s));
            _all.Add(booking);
            return true;
        }
    }
}

// ═════════════════════════════════════════════════════════════════════════════════════════════
// OBSERVER PATTERN, in plain English
//   After a sale, several things may want to react: send an email, send an SMS, update analytics.
//   The booking code must not know about any of them. It just says "a booking was confirmed" to
//   whoever subscribed. Adding a new reaction = adding a new listener, with zero edits to booking code.
// ═════════════════════════════════════════════════════════════════════════════════════════════
public interface IBookingListener { void OnConfirmed(Booking booking); }

/// <summary>A listener that just records what happened, so tests can check notifications were sent once.</summary>
public sealed class RecordingListener : IBookingListener
{
    private readonly List<string> _events = new();
    public IReadOnlyList<string> Events { get { lock (_events) return _events.ToList(); } }
    public void OnConfirmed(Booking b) { lock (_events) _events.Add($"ticket for {b.UserId}: {string.Join(",", b.SeatIds)}"); }
}
