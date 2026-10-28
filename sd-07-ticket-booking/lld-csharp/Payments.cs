namespace TicketBooking;

public sealed record PaymentResult(bool Success, string PaymentId, string? Error);

public interface IPaymentGateway
{
    // ┌─ IDEMPOTENCY, in plain English ─────────────────────────────────────────────────────────────┐
    // │ Your phone shows a spinner, the network drops, you tap Pay again. Without protection the bank  │
    // │ might charge twice. With an IDEMPOTENCY KEY (a unique id for this checkout) the provider       │
    // │ remembers "I already handled key X" and simply returns the SAME answer the second time.        │
    // │ "Idempotent" = doing it twice has the same effect as doing it once.                            │
    // └────────────────────────────────────────────────────────────────────────────────────────────────┘
    PaymentResult Charge(string idempotencyKey, decimal amount);
    void Refund(string paymentId);
}

/// <summary>Pretend payment provider for tests. Flip FailNextCharge to simulate a declined card.</summary>
public sealed class FakePaymentGateway : IPaymentGateway
{
    private readonly object _gate = new();                       // the "bathroom key" protecting everything below
    private readonly Dictionary<string, PaymentResult> _byKey = new();   // key → result we already produced
    private readonly HashSet<string> _refunded = new();          // a HashSet silently ignores duplicates → refunds are idempotent too

    public int ChargeCount { get; private set; }                 // tests check "was the card charged exactly once?"
    public int RefundCount => _refunded.Count;
    public bool FailNextCharge { get; set; }

    public PaymentResult Charge(string idempotencyKey, decimal amount)
    {
        // Two threads sending the SAME key at the SAME time must not both charge: one waits for the key,
        // then finds the first one's answer already stored and returns it. That is the whole trick.
        lock (_gate)
        {
            if (_byKey.TryGetValue(idempotencyKey, out var existing)) return existing;   // replay → same answer, no new charge

            PaymentResult result;
            if (FailNextCharge) { FailNextCharge = false; result = new(false, "", "card declined"); }
            else { ChargeCount++; result = new(true, $"pay-{ChargeCount}", null); }

            _byKey[idempotencyKey] = result;                     // remember it so a retry gets the same result
            return result;
        }
    }

    public void Refund(string paymentId) { lock (_gate) _refunded.Add(paymentId); }
}
