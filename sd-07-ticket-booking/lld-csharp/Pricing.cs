namespace TicketBooking;

// ═════════════════════════════════════════════════════════════════════════════════════════════
// STRATEGY PATTERN, in plain English
//   "How much does this seat cost?" is a RULE that changes over time (weekend prices, coupons, sales).
//   If that rule lived inside BookingService as a pile of `if`s, every new rule would mean editing
//   (and risking) the booking code. Instead each rule is a small class behind ONE interface, and the
//   service just calls `PriceFor(...)` without knowing which rule it got.
//   → Open/Closed principle: add behaviour by ADDING a class, never by editing old ones.
// ═════════════════════════════════════════════════════════════════════════════════════════════
public interface IPricingStrategy { decimal PriceFor(Show show, Seat seat); }

// `(IReadOnlyDictionary<...> prices)` is a primary constructor; `prices` can be used in the body.
public sealed class SeatTypePricing(IReadOnlyDictionary<SeatType, decimal> prices) : IPricingStrategy
{
    public decimal PriceFor(Show show, Seat seat) => prices[seat.Type];   // lookup by seat type
}

// ═════════════════════════════════════════════════════════════════════════════════════════════
// DECORATOR PATTERN, in plain English
//   A decorator WRAPS another strategy and adds one tweak, like putting a gift wrapper on a gift.
//   WeekendSurchargePricing asks the inner strategy for the base price, then adds 20% on weekends.
//   Wrappers stack: new DiscountPricing(new WeekendSurchargePricing(new SeatTypePricing(...))).
// ═════════════════════════════════════════════════════════════════════════════════════════════
public sealed class WeekendSurchargePricing(IPricingStrategy inner, decimal surchargePercent) : IPricingStrategy
{
    public decimal PriceFor(Show show, Seat seat)
    {
        var basePrice = inner.PriceFor(show, seat);                                           // ask the wrapped rule first
        var weekend = show.StartsAtUtc.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;   // `is A or B` = "either one"
        return weekend ? basePrice * (1 + surchargePercent / 100m) : basePrice;
        // `100m` : the `m` suffix means decimal. Always use decimal (never double) for money.
    }
}
