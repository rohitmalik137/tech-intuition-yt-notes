// Self-checking demo: each scenario proves ONE guarantee, and throws if it is ever broken.
// Run:  dotnet run
using QuickCommerce;

var passed = 0;
void Check(bool condition, string what)
{
    if (!condition) throw new Exception("FAILED: " + what);
    Console.WriteLine("  ✔ " + what);
    passed++;
}
var T0 = new DateTime(2026, 1, 1, 20, 0, 0, DateTimeKind.Utc);
IReadOnlyList<OrderLine> Lines(params (string p, int q)[] xs) => xs.Select(x => new OrderLine(x.p, x.q)).ToList();

// ---------------------------------------------------------------------------------------------
Console.WriteLine("1. Last packet of milk, 20 buyers at the same instant");
{
    var inv = new StoreInventory(new FakeClock(T0), new Dictionary<string, int> { ["milk"] = 1 });
    var wins = 0;
    Parallel.For(0, 20, new ParallelOptions { MaxDegreeOfParallelism = 20 }, i =>
    {
        if (inv.TryReserve($"buyer{i}", Lines(("milk", 1)), TimeSpan.FromMinutes(5))) Interlocked.Increment(ref wins);
    });
    Check(wins == 1, "exactly one buyer wins the last unit");
    Check(inv.Available("milk") == 0, "stock is 0, never negative");
}

// ---------------------------------------------------------------------------------------------
Console.WriteLine("2. All or nothing across items");
{
    var inv = new StoreInventory(new FakeClock(T0), new Dictionary<string, int> { ["milk"] = 5, ["eggs"] = 0 });
    var ok = inv.TryReserve("A", Lines(("milk", 1), ("eggs", 1)), TimeSpan.FromMinutes(5));
    Check(!ok, "order is refused because eggs are gone");
    Check(inv.Available("milk") == 5, "milk was NOT taken (no half-reservation)");
}

// ---------------------------------------------------------------------------------------------
Console.WriteLine("3. Opposite item order does not deadlock");
{
    var inv = new StoreInventory(new FakeClock(T0), new Dictionary<string, int> { ["milk"] = 1000, ["bread"] = 1000 });
    var t1 = Task.Run(() => { for (var i = 0; i < 2000; i++) { inv.TryReserve($"x{i}", Lines(("milk", 1), ("bread", 1)), TimeSpan.FromMinutes(5)); inv.Release($"x{i}"); } });
    var t2 = Task.Run(() => { for (var i = 0; i < 2000; i++) { inv.TryReserve($"y{i}", Lines(("bread", 1), ("milk", 1)), TimeSpan.FromMinutes(5)); inv.Release($"y{i}"); } });
    Check(Task.WaitAll([t1, t2], TimeSpan.FromSeconds(10)), "both finished (sorted lock order)");
    Check(inv.Available("milk") == 1000 && inv.Available("bread") == 1000, "all stock returned exactly once");
}

// ---------------------------------------------------------------------------------------------
Console.WriteLine("4. Hold expiry releases exactly once");
{
    var clock = new FakeClock(T0);
    var inv = new StoreInventory(clock, new Dictionary<string, int> { ["milk"] = 1 });
    inv.TryReserve("O1", Lines(("milk", 1)), TimeSpan.FromMinutes(5));
    clock.Advance(TimeSpan.FromMinutes(6));
    Check(!inv.Confirm("O1"), "an expired hold cannot be confirmed");
    Check(inv.Tick() == 1 && inv.Available("milk") == 1, "cleanup returns the unit");
    Check(inv.Tick() == 0 && inv.Available("milk") == 1, "running cleanup again adds nothing (no double release)");
}

// ---------------------------------------------------------------------------------------------
Console.WriteLine("5. Same Idempotency-Key from 8 retrying threads");
{
    var gateway = new ScriptedGateway();
    var inv = new StoreInventory(new FakeClock(T0), new Dictionary<string, int> { ["milk"] = 10 });
    var svc = new OrderService(inv, gateway);
    var orders = new Order[8];
    Parallel.For(0, 8, i => orders[i] = svc.PlaceOrder("key-123", "store17", Lines(("milk", 2)), 12000));
    Check(orders.All(o => ReferenceEquals(o, orders[0])), "every retry got the SAME order");
    Check(gateway.Charges == 1, "the customer was charged once");
    Check(inv.Available("milk") == 8 && orders[0].Status == OrderStatus.Confirmed, "stock reserved once, order confirmed");
}

// ---------------------------------------------------------------------------------------------
Console.WriteLine("6. Payment times out, hold expires, THEN payment succeeds");
{
    var clock = new FakeClock(T0);
    var gateway = new ScriptedGateway { Next = PaymentOutcome.Unknown, QueryResult = PaymentOutcome.Success };
    var inv = new StoreInventory(clock, new Dictionary<string, int> { ["milk"] = 1 });
    var svc = new OrderService(inv, gateway);

    var order = svc.PlaceOrder("k1", "store17", Lines(("milk", 1)), 6000);
    Check(order.Status == OrderStatus.PaymentUnknown, "timeout is 'unknown', not 'failed'");

    clock.Advance(TimeSpan.FromMinutes(6)); inv.Tick();
    svc.Reconcile(order);
    Check(order.Status == OrderStatus.Confirmed && inv.Available("milk") == 0, "stock still there: took a fresh reservation and confirmed");

    // Variant: someone else grabs the milk before reconciliation runs -> refund.
    var gateway2 = new ScriptedGateway { Next = PaymentOutcome.Unknown, QueryResult = PaymentOutcome.Success };
    var inv2 = new StoreInventory(clock, new Dictionary<string, int> { ["milk"] = 1 });
    var svc2 = new OrderService(inv2, gateway2);
    var order2 = svc2.PlaceOrder("k2", "store17", Lines(("milk", 1)), 6000);
    clock.Advance(TimeSpan.FromMinutes(6)); inv2.Tick();
    inv2.TryReserve("someone-else", Lines(("milk", 1)), TimeSpan.FromMinutes(5));
    svc2.Reconcile(order2);
    Check(order2.Status == OrderStatus.Refunded && gateway2.Refunds == 1, "no stock left: the customer is refunded");
}

// ---------------------------------------------------------------------------------------------
Console.WriteLine("7. Order state machine rejects stale and illegal moves");
{
    var o = new Order("O9", "store17", Lines(("milk", 1)), 6000);
    Check(o.TryMove(OrderStatus.PaymentPending, 0), "Created -> PaymentPending");
    Check(!o.TryMove(OrderStatus.Delivered, 1), "cannot jump to Delivered");
    Check(!o.TryMove(OrderStatus.Confirmed, 0), "a stale event (old version) is ignored");
    Check(o.TryMove(OrderStatus.Confirmed, 1) && o.Version == 2, "the correct move works and bumps the version");
}

// ---------------------------------------------------------------------------------------------
Console.WriteLine("8. Rider claim guards BOTH the rider and the order");
{
    var clock = new FakeClock(T0);

    // 8a. One free rider, two orders at once -> only one order gets her.
    var one = new DispatchService([new Rider("R1", 0, 0)], new NearestWithLightLoad(), clock);
    var results = new Rider?[2];
    Parallel.For(0, 2, i => results[i] = one.TryAssign($"order{i}", 0, 0));
    Check(results.Count(r => r is not null) == 1, "one rider can't be double-booked");

    // 8b. Two workers race to assign the SAME order -> still exactly one rider.
    var two = new DispatchService([new Rider("R1", 0, 1), new Rider("R2", 0, 2)], new NearestWithLightLoad(), clock);
    var got = new Rider?[2];
    Parallel.For(0, 2, i => got[i] = two.TryAssign("orderX", 0, 0));
    Check(got.Count(r => r is not null) == 1, "one order can't get two riders");

    // 8c. Lease: nobody accepts in time -> the claim is undone and a new rider can be tried.
    var rider = new Rider("R9", 0, 0);
    var svc = new DispatchService([rider], new NearestWithLightLoad(), clock);
    svc.TryAssign("orderZ", 0, 0);
    clock.Advance(TimeSpan.FromSeconds(20));
    Check(!svc.Accept(rider), "accepting after the lease expired fails");
    Check(svc.ExpireLeases() == 1 && rider.Status == RiderStatus.Free, "expired claim is released");
    Check(svc.TryAssign("orderZ", 0, 0) is not null, "the order can be assigned again");
}

// ---------------------------------------------------------------------------------------------
Console.WriteLine("9. Tracking keeps only the latest position");
{
    var loc = new RiderLocations();
    loc.Update("R1", 1, 1, seq: 5);
    loc.Update("R1", 9, 9, seq: 3);          // arrives late: older, must be ignored
    Check(loc.Get("R1") is { Seq: 5, X: 1 }, "the late ping did not overwrite the newer one");
}

Console.WriteLine($"\nAll good: {passed} checks passed.");

// A scriptable fake payment provider.
class ScriptedGateway : IPaymentGateway
{
    public PaymentOutcome Next = PaymentOutcome.Success;
    public PaymentOutcome QueryResult = PaymentOutcome.Success;
    public int Charges, Refunds;
    public PaymentOutcome Charge(string paymentKey, int amountPaise) { Interlocked.Increment(ref Charges); return Next; }
    public PaymentOutcome Query(string paymentKey) => QueryResult;
    public void Refund(string paymentKey) => Interlocked.Increment(ref Refunds);
}
