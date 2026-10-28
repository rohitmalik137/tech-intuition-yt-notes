using TicketBooking;

// A self-checking demo: each scenario prints PASS/FAIL. Run with:  dotnet run
//
// HOW TO READ THIS FILE: it uses "top-level statements" (no Main method, no class). The code simply runs
// from top to bottom. Each `{ ... }` block below is an independent scenario with its own fresh service,
// so one scenario can never affect another.
var failures = 0;
void Check(bool ok, string what) { Console.WriteLine($"{(ok ? "PASS" : "FAIL")}  {what}"); if (!ok) failures++; }

static Show MakeShow(string id, DateTime startsAt) => new(id, "Coldplay Live", startsAt,
    new[] { "A", "B" }.SelectMany(row => Enumerable.Range(1, 6).Select(n =>
        new Seat($"{row}{n}", row == "A" ? SeatType.Premium : SeatType.Regular))));

static (BookingService Svc, FakeClock Clock, FakePaymentGateway Pay, InMemoryBookingRepository Repo, RecordingListener Mail) Build(DateTime startsAt)
{
    var clock = new FakeClock(new DateTime(2026, 10, 1, 11, 59, 0, DateTimeKind.Utc));
    var pay = new FakePaymentGateway();
    var repo = new InMemoryBookingRepository();
    var mail = new RecordingListener();
    var pricing = new WeekendSurchargePricing(
        new SeatTypePricing(new Dictionary<SeatType, decimal> { [SeatType.Regular] = 2500m, [SeatType.Premium] = 6000m, [SeatType.Recliner] = 9000m }), 20m);
    var svc = new BookingService(clock, pay, repo, pricing, new IBookingListener[] { mail }, TimeSpan.FromMinutes(8));
    svc.AddShow(MakeShow("show-1", startsAt));
    return (svc, clock, pay, repo, mail);
}

var weekday = new DateTime(2026, 10, 7, 14, 0, 0, DateTimeKind.Utc);   // Wednesday
var weekend = new DateTime(2026, 10, 10, 14, 0, 0, DateTimeKind.Utc);  // Saturday

// 1. Two fans race for the same seat → exactly one winner.
//    A Barrier makes both tasks WAIT until both are ready, then releases them at the same instant, so the
//    race is real instead of "task 1 happened to finish before task 2 started".
{
    var (svc, _, _, _, _) = Build(weekday);
    using var barrier = new Barrier(2);
    var results = new[] { "fan-1", "fan-2" }
        .Select(u => Task.Run(() => { barrier.SignalAndWait(); return svc.CreateHold(u, "show-1", new[] { "A1" }); }))
        .Select(t => t.Result).ToList();
    Check(results.Count(r => r.Ok) == 1, "race for A1: exactly one hold wins");
}

// 2. Hundreds of racers, many seats: no seat is ever held twice.
//    Parallel.For runs the loop body on many threads at once. ConcurrentBag is a thread-safe collection.
//    400 people fight over 6 seats; exactly 6 must win and each seat must be won by exactly one person.
{
    var (svc, _, _, _, _) = Build(weekday);
    var wins = new System.Collections.Concurrent.ConcurrentBag<string>();
    Parallel.For(0, 400, i =>
    {
        var seat = $"B{1 + i % 6}";
        var r = svc.CreateHold($"fan-{i}", "show-1", new[] { seat });
        if (r.Ok) wins.Add(seat);
    });
    Check(wins.Count == 6 && wins.Distinct().Count() == 6, "400 racers over 6 seats: exactly 6 winners, one per seat");
}

// 3. Multi-seat is all-or-nothing: an overlapping request leaves NO half-held seats behind.
{
    var (svc, _, _, _, _) = Build(weekday);
    var a = svc.CreateHold("fan-a", "show-1", new[] { "B1", "B2", "B3" });
    var b = svc.CreateHold("fan-b", "show-1", new[] { "B3", "B4" });
    var c = svc.CreateHold("fan-c", "show-1", new[] { "B4" });
    Check(a.Ok && !b.Ok && c.Ok, "overlap on B3: second request fails as a set, B4 stays free");
}

// 4. Idempotent confirm: a double tap charges once and returns the same booking.
{
    var (svc, _, pay, repo, mail) = Build(weekday);
    var hold = svc.CreateHold("fan-a", "show-1", new[] { "A1", "A2" }).Value!;
    var first = svc.Confirm(hold.HoldId, "checkout-123");
    var second = svc.Confirm(hold.HoldId, "checkout-123");
    Check(first.Ok && second.Ok && first.Value!.BookingId == second.Value!.BookingId, "double tap returns the same booking");
    Check(pay.ChargeCount == 1 && repo.All.Count == 1 && mail.Events.Count == 1, "…with one charge, one booking, one notification");
    Check(first.Value!.Amount == 12000m, "2 premium seats on a weekday = 12,000");
}

// 5. Expiry is lazy: after 8 minutes the seat is free again, and a late confirm is refused.
{
    var (svc, clock, pay, _, _) = Build(weekday);
    var hold = svc.CreateHold("fan-a", "show-1", new[] { "B5" }).Value!;
    clock.Advance(TimeSpan.FromMinutes(8) + TimeSpan.FromSeconds(1));
    var other = svc.CreateHold("fan-b", "show-1", new[] { "B5" });
    var late = svc.Confirm(hold.HoldId, "checkout-late");
    Check(other.Ok, "after the timer, someone else can take the seat");
    Check(!late.Ok && pay.ChargeCount == 0, "late confirm is refused before any money moves");
}

// 6. Payment declined → the hold is released at once.
{
    var (svc, _, pay, _, _) = Build(weekday);
    var hold = svc.CreateHold("fan-a", "show-1", new[] { "B6" }).Value!;
    pay.FailNextCharge = true;
    var res = svc.Confirm(hold.HoldId, "checkout-bad");
    var retry = svc.CreateHold("fan-b", "show-1", new[] { "B6" });
    Check(!res.Ok && retry.Ok, "declined card frees the seat immediately");
}

// 7. Pricing decorator: weekend surcharge stacks on the base strategy without editing it.
{
    var (svc, _, _, _, _) = Build(weekend);
    var hold = svc.CreateHold("fan-a", "show-1", new[] { "B1" }).Value!;
    var res = svc.Confirm(hold.HoldId, "checkout-wknd");
    Check(res.Ok && res.Value!.Amount == 3000m, "regular seat on a Saturday = 2,500 + 20% = 3,000");
}

Console.WriteLine(failures == 0 ? "\nAll scenarios passed." : $"\n{failures} scenario(s) FAILED.");
return failures;
