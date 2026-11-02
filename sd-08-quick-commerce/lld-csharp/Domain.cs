namespace QuickCommerce;

// Everything outside the process sits behind an interface, so tests can fake it.
public interface IClock { DateTime UtcNow { get; } }
public sealed class SystemClock : IClock { public DateTime UtcNow => DateTime.UtcNow; }
public sealed class FakeClock(DateTime start) : IClock
{
    private DateTime _now = start;
    public DateTime UtcNow => _now;
    public void Advance(TimeSpan by) => _now += by;
}

/// <summary>One line of an order: "2 × milk". A record = an immutable bag of data with value equality.</summary>
public sealed record OrderLine(string ProductId, int Qty);

public enum OrderStatus
{
    Created, PaymentPending, PaymentUnknown, Confirmed,
    Picking, Packed, OutForDelivery, Delivered, Cancelled, Refunded
}

/// <summary>
/// An order is a STATE MACHINE with a VERSION number.
///  - The table <see cref="Allowed"/> is the whole business rule: "from X you may only go to Y or Z".
///  - The version is the guard against stale or duplicate events. An event says "I saw version 3".
///    If the order is already at version 4, the event is old news and is rejected.
/// This is the C# twin of:  UPDATE orders SET status=?, version=version+1 WHERE id=? AND version=?
/// </summary>
public sealed class Order(string id, string storeId, IReadOnlyList<OrderLine> lines, int amountPaise)
{
    public string Id { get; } = id;
    public string StoreId { get; } = storeId;
    public IReadOnlyList<OrderLine> Lines { get; } = lines;
    public int AmountPaise { get; } = amountPaise;

    // One private lock object so status and version always change TOGETHER (never half-updated).
    private readonly object _gate = new();
    private OrderStatus _status = OrderStatus.Created;
    private int _version;

    public OrderStatus Status { get { lock (_gate) return _status; } }
    public int Version { get { lock (_gate) return _version; } }

    // Allowed transitions. Terminal states (Delivered, Cancelled, Refunded) have no exits.
    private static readonly Dictionary<OrderStatus, OrderStatus[]> Allowed = new()
    {
        [OrderStatus.Created]        = [OrderStatus.PaymentPending, OrderStatus.Cancelled],
        [OrderStatus.PaymentPending] = [OrderStatus.Confirmed, OrderStatus.PaymentUnknown, OrderStatus.Cancelled, OrderStatus.Refunded],
        [OrderStatus.PaymentUnknown] = [OrderStatus.Confirmed, OrderStatus.Refunded, OrderStatus.Cancelled],
        [OrderStatus.Confirmed]      = [OrderStatus.Picking, OrderStatus.Cancelled],
        [OrderStatus.Picking]        = [OrderStatus.Packed, OrderStatus.Cancelled],
        [OrderStatus.Packed]         = [OrderStatus.OutForDelivery],
        [OrderStatus.OutForDelivery] = [OrderStatus.Delivered],
        [OrderStatus.Delivered]      = [],
        [OrderStatus.Cancelled]      = [],
        [OrderStatus.Refunded]       = [],
    };

    /// <summary>Move to <paramref name="to"/> only if the version still matches AND the move is legal.</summary>
    /// <returns>true if THIS call changed the order; false if it was stale or illegal (caller ignores it).</returns>
    public bool TryMove(OrderStatus to, int expectedVersion)
    {
        lock (_gate)
        {
            if (_version != expectedVersion) return false;           // stale or duplicate event
            if (!Allowed[_status].Contains(to)) return false;        // illegal jump, e.g. Delivered -> Picking
            _status = to;
            _version++;
            return true;
        }
    }
}
