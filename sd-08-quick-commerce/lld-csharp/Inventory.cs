namespace QuickCommerce;

public enum HoldStatus { Held, Confirmed, Released }

/// <summary>
/// A hold = "these units are set aside for this order until ExpiresAt".
/// Its status may move away from Held exactly ONCE. TryMove returns true only for the caller that
/// made the move, so "release the stock back" can only ever happen once.
/// (SQL twin:  UPDATE reservations SET status='RELEASED' WHERE id=? AND status='HELD')
/// </summary>
public sealed class Hold(string orderId, IReadOnlyList<OrderLine> sortedLines, DateTime expiresAt)
{
    public string OrderId { get; } = orderId;
    public IReadOnlyList<OrderLine> Lines { get; } = sortedLines;
    public DateTime ExpiresAt { get; } = expiresAt;

    private readonly object _gate = new();
    private HoldStatus _status = HoldStatus.Held;
    public HoldStatus Status { get { lock (_gate) return _status; } }

    public bool TryMove(HoldStatus to)
    {
        lock (_gate)
        {
            if (_status != HoldStatus.Held) return false;   // someone already confirmed or released it
            _status = to;
            return true;
        }
    }
}

/// <summary>
/// The stock of ONE dark store. This class is the in-memory twin of the inventory DATABASE:
/// the single authority for "how many are left". A Redis cache, if any, would only ever read from it.
///
/// LOCK ORDER (written down so nobody introduces a deadlock later):
///     product locks (sorted by product id)  ->  _holds  ->  a hold's own gate.
/// Nothing ever takes them in the opposite direction.
/// </summary>
public sealed class StoreInventory
{
    // One tiny object per product: its count AND the lock that protects that count.
    // (DB twin: one ROW per (store, product); the database locks the row for us.)
    private sealed class Stock { public int Available; }

    private readonly Dictionary<string, Stock> _stock;          // read-only shape after construction
    private readonly Dictionary<string, Hold> _holds = new();   // orderId -> hold, guarded by lock(_holds)
    private readonly List<string> _audit = new();                // cycle-count corrections
    private readonly IClock _clock;

    public StoreInventory(IClock clock, IReadOnlyDictionary<string, int> initial)
    {
        _clock = clock;
        _stock = initial.ToDictionary(kv => kv.Key, kv => new Stock { Available = kv.Value });
    }

    public int Available(string productId)
    {
        var s = _stock[productId];
        lock (s) return s.Available;
    }

    /// <summary>
    /// Reserve ALL lines or NOTHING. Idempotent: asking again for an order that already holds stock is a no-op "true".
    ///
    /// The three-step recipe (same as the seat hold in the ticket note):
    ///   1. lock every product involved, in SORTED order  (sorted = no deadlock)
    ///   2. PLAN: check every line has enough stock        (nothing changed yet)
    ///   3. APPLY: subtract everything                     (cannot fail now, because we hold the locks)
    /// </summary>
    public bool TryReserve(string orderId, IReadOnlyList<OrderLine> lines, TimeSpan ttl)
    {
        // Reject nonsense before touching any lock.
        if (lines.Count == 0 || lines.Any(l => l.Qty <= 0 || !_stock.ContainsKey(l.ProductId))) return false;

        // Idempotency: same order asking twice must not take stock twice.
        Hold? existing;
        lock (_holds) _holds.TryGetValue(orderId, out existing);
        if (existing is not null)
        {
            if (existing.Status == HoldStatus.Confirmed) return true;
            if (existing.Status == HoldStatus.Held && _clock.UtcNow < existing.ExpiresAt) return true;
            if (existing.Status == HoldStatus.Held) Release(orderId);   // expired but not swept yet: free it first
            // Status is Released: fall through and take a FRESH reservation (used by payment reconciliation).
        }

        var sorted = lines.OrderBy(l => l.ProductId, StringComparer.Ordinal).ToList();

        return WithLocks(sorted, 0, () =>
        {
            // PLAN: look only.
            foreach (var l in sorted)
                if (_stock[l.ProductId].Available < l.Qty) return false;

            // APPLY: now it is safe to change everything.
            foreach (var l in sorted)
                _stock[l.ProductId].Available -= l.Qty;

            var hold = new Hold(orderId, sorted, _clock.UtcNow + ttl);
            lock (_holds) _holds[orderId] = hold;   // brief lock, taken AFTER product locks (see LOCK ORDER)
            return true;
        });
    }

    /// <summary>Payment succeeded: make the hold permanent. An expired hold can NOT be confirmed.</summary>
    public bool Confirm(string orderId)
    {
        Hold? h;
        lock (_holds) _holds.TryGetValue(orderId, out h);
        if (h is null) return false;
        if (_clock.UtcNow >= h.ExpiresAt) return false;   // lazy expiry: the clock says it is dead, whether or not a sweep ran
        return h.TryMove(HoldStatus.Confirmed);
    }

    /// <summary>Give the units back. Returns true only for the ONE caller that actually released.</summary>
    public bool Release(string orderId)
    {
        Hold? h;
        lock (_holds) _holds.TryGetValue(orderId, out h);
        if (h is null) return false;

        return WithLocks(h.Lines, 0, () =>
        {
            if (!h.TryMove(HoldStatus.Released)) return false;   // already confirmed or released: do nothing, add nothing back
            foreach (var l in h.Lines) _stock[l.ProductId].Available += l.Qty;
            return true;
        });
    }

    /// <summary>The periodic cleanup job: release every hold whose time is up.</summary>
    public int Tick()
    {
        List<Hold> due;
        lock (_holds)
            due = _holds.Values.Where(h => h.Status == HoldStatus.Held && h.ExpiresAt <= _clock.UtcNow).ToList();
        // The lock is dropped BEFORE releasing, so we never hold _holds while taking product locks.
        return due.Count(h => Release(h.OrderId));
    }

    /// <summary>Mother Hub delivered stock.</summary>
    public void Restock(string productId, int qty)
    {
        var s = _stock[productId];
        lock (s) s.Available += qty;
    }

    /// <summary>Shelf says <paramref name="counted"/>, system says otherwise: trust the shelf, keep an audit line.</summary>
    public void CycleCount(string productId, int counted)
    {
        var s = _stock[productId];
        lock (s)
        {
            var before = s.Available;
            s.Available = counted;
            lock (_audit) _audit.Add($"{productId}: system {before} -> shelf {counted}");
        }
    }

    // ┌─ SYNTAX NOTE: recursion to hold N locks ───────────────────────────────────────────────────────┐
    // │ `lock` is a block, so you cannot "lock a list" in a loop. Recursion nests one lock per product: │
    // │   lock(a){ lock(b){ lock(c){ body() } } }                                                       │
    // │ `Func<bool> body` is a piece of code passed in as a value, run only once every lock is held.    │
    // └──────────────────────────────────────────────────────────────────────────────────────────────────┘
    private bool WithLocks(IReadOnlyList<OrderLine> sortedLines, int i, Func<bool> body)
    {
        if (i == sortedLines.Count) return body();
        var stock = _stock[sortedLines[i].ProductId];
        lock (stock) return WithLocks(sortedLines, i + 1, body);
    }
}
