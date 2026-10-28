namespace TicketBooking;

// ── Strategy: how a seat is priced is a policy you can swap without touching booking code ─────
public interface IPricingStrategy { decimal PriceFor(Show show, Seat seat); }

public sealed class SeatTypePricing(IReadOnlyDictionary<SeatType, decimal> prices) : IPricingStrategy
{
    public decimal PriceFor(Show show, Seat seat) => prices[seat.Type];
}

// ── Decorator: layer a rule on top of another strategy (open/closed: add rules, don't edit old ones) ──
public sealed class WeekendSurchargePricing(IPricingStrategy inner, decimal surchargePercent) : IPricingStrategy
{
    public decimal PriceFor(Show show, Seat seat)
    {
        var basePrice = inner.PriceFor(show, seat);
        var weekend = show.StartsAtUtc.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
        return weekend ? basePrice * (1 + surchargePercent / 100m) : basePrice;
    }
}
