using System.Collections.Concurrent;

namespace QuickCommerce;

public enum RiderStatus { Free, Claimed, Busy }

public sealed class Rider(string id, double x, double y)
{
    public string Id { get; } = id;
    public double X { get; } = x;
    public double Y { get; } = y;
    public int ActiveOrders { get; set; }

    // Rider-level lock + fields it protects.
    public readonly object Gate = new();
    public RiderStatus Status = RiderStatus.Free;
    public string? OrderId;                 // which order has claimed me
    public DateTime LeaseExpiresAt;         // if the rider doesn't accept by then, the claim is released
}

/// <summary>STRATEGY pattern: how to rank riders is swappable. Lower score = better.</summary>
public interface IRiderScoring { double Score(Rider rider, double storeX, double storeY); }

public sealed class NearestWithLightLoad : IRiderScoring
{
    public double Score(Rider r, double sx, double sy)
    {
        var distance = Math.Sqrt((r.X - sx) * (r.X - sx) + (r.Y - sy) * (r.Y - sy));
        return distance + 2.0 * r.ActiveOrders;     // a loaded rider costs "two extra units of distance" per order
    }
}

public enum ClaimResult { Ok, RiderTaken, OrderTaken }

/// <summary>
/// Assigns riders to orders. The only hard part is the CLAIM: it must guard BOTH sides.
///   guard 1: this rider is still Free
///   guard 2: this order is still unassigned (at most ONE active assignment per order)
/// SQL twin: one transaction updating riders AND assignments, with a unique index on active (order_id).
/// </summary>
public sealed class DispatchService(IReadOnlyList<Rider> riders, IRiderScoring scoring, IClock clock)
{
    private static readonly TimeSpan Lease = TimeSpan.FromSeconds(15);

    // orderId -> riderId. TryAdd is atomic: two threads adding the same orderId, exactly one wins.
    // This IS the "unique active assignment per order" constraint.
    private readonly ConcurrentDictionary<string, string> _assigned = new();

    public Rider? TryAssign(string orderId, double storeX, double storeY)
    {
        // Rank the riders that LOOK free. This read is only a hint: the claim below re-checks under the lock.
        var ranked = riders.Where(r => r.Status == RiderStatus.Free)
                           .OrderBy(r => scoring.Score(r, storeX, storeY))
                           .ToList();

        foreach (var rider in ranked)
        {
            switch (Claim(orderId, rider))
            {
                case ClaimResult.Ok:         return rider;
                case ClaimResult.OrderTaken: return null;   // someone else already assigned this order: stop
                case ClaimResult.RiderTaken: continue;      // that rider got snatched: try the next best
            }
        }
        return null;                                        // nobody free right now
    }

    private ClaimResult Claim(string orderId, Rider rider)
    {
        lock (rider.Gate)                                   // the check and the change happen with no gap between
        {
            if (rider.Status != RiderStatus.Free) return ClaimResult.RiderTaken;        // guard 1
            if (!_assigned.TryAdd(orderId, rider.Id)) return ClaimResult.OrderTaken;    // guard 2
            rider.Status = RiderStatus.Claimed;
            rider.OrderId = orderId;
            rider.LeaseExpiresAt = clock.UtcNow + Lease;
            return ClaimResult.Ok;
        }
    }

    /// <summary>The rider tapped "accept" in time: the claim becomes a real job.</summary>
    public bool Accept(Rider rider)
    {
        lock (rider.Gate)
        {
            if (rider.Status != RiderStatus.Claimed || clock.UtcNow >= rider.LeaseExpiresAt) return false;
            rider.Status = RiderStatus.Busy;
            rider.ActiveOrders++;
            return true;
        }
    }

    /// <summary>Recovery: any claim whose lease ran out is undone, so the next rider can be tried.</summary>
    public int ExpireLeases()
    {
        var released = 0;
        foreach (var rider in riders)
        {
            lock (rider.Gate)
            {
                if (rider.Status == RiderStatus.Claimed && clock.UtcNow >= rider.LeaseExpiresAt)
                {
                    _assigned.TryRemove(rider.OrderId!, out _);   // `!` = "I know it is not null here"; `out _` = ignore the result
                    rider.Status = RiderStatus.Free;
                    rider.OrderId = null;
                    released++;
                }
            }
        }
        return released;
    }
}

/// <summary>
/// "Latest position only". Pings can arrive out of order; each carries a sequence number and an older one is ignored.
/// 25,000 pings a second are NOT stored: each one overwrites the previous value for that rider.
/// </summary>
public sealed class RiderLocations
{
    private readonly ConcurrentDictionary<string, (double X, double Y, long Seq)> _latest = new();

    public void Update(string riderId, double x, double y, long seq)
        => _latest.AddOrUpdate(riderId,
               _ => (x, y, seq),                                          // first ping for this rider
               (_, old) => seq > old.Seq ? (x, y, seq) : old);            // keep whichever is NEWER

    public (double X, double Y, long Seq)? Get(string riderId)
        => _latest.TryGetValue(riderId, out var v) ? v : null;
}
