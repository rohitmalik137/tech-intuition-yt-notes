using System.Collections.Concurrent;

namespace QuickCommerce;

public enum PaymentOutcome { Success, Declined, Unknown }

/// <summary>
/// The outside world. Real life: Razorpay, a bank, UPI... Here: an interface we can fake.
/// KEY IDEA: every call carries a STABLE payment key. Retrying Charge with the same key can never charge twice.
/// </summary>
public interface IPaymentGateway
{
    PaymentOutcome Charge(string paymentKey, int amountPaise);
    PaymentOutcome Query(string paymentKey);      // "what happened to that charge?"  (used after a timeout)
    void Refund(string paymentKey);
}

/// <summary>
/// The SAGA for checkout. Each step has an undo:
///   save PENDING first  ->  reserve stock  ->  charge (stable key)  ->  confirm
///   undo:  cancel            release          refund
/// </summary>
public sealed class OrderService(StoreInventory inventory, IPaymentGateway payments)
{
    private static readonly TimeSpan HoldTtl = TimeSpan.FromMinutes(5);

    // idempotency key -> the order it created.
    // Lazy<T> = "run this at most once, even if 8 threads ask at the same time; everyone gets the same result".
    private readonly ConcurrentDictionary<string, Lazy<Order>> _byKey = new();
    private int _nextId;

    // Observer: other services (fulfilment, dispatch, notification) subscribe. In production this is the queue + outbox.
    public event Action<Order>? OrderConfirmed;

    /// <summary>Retrying with the same key returns the SAME order: never a second one, never a second charge.</summary>
    public Order PlaceOrder(string idempotencyKey, string storeId, IReadOnlyList<OrderLine> lines, int amountPaise)
        => _byKey.GetOrAdd(idempotencyKey, _ => new Lazy<Order>(() => Run(storeId, lines, amountPaise))).Value;

    private Order Run(string storeId, IReadOnlyList<OrderLine> lines, int amountPaise)
    {
        var order = new Order($"O{Interlocked.Increment(ref _nextId)}", storeId, lines, amountPaise);

        // STEP 1. Record intent FIRST. If we crash after charging, recovery has something to work from.
        Advance(order, OrderStatus.PaymentPending);

        // STEP 2. Reserve. If any item is gone we refuse BEFORE taking money.
        if (!inventory.TryReserve(order.Id, lines, HoldTtl))
        {
            Advance(order, OrderStatus.Cancelled);
            return order;
        }

        // STEP 3. Charge with a stable key derived from the order id.
        var outcome = payments.Charge(PaymentKey(order), amountPaise);
        Settle(order, outcome);
        return order;
    }

    // Three outcomes, three different reactions. "Unknown" is NOT "failed".
    private void Settle(Order order, PaymentOutcome outcome)
    {
        switch (outcome)
        {
            case PaymentOutcome.Success:
                ConfirmPaid(order);
                break;
            case PaymentOutcome.Declined:
                inventory.Release(order.Id);
                Advance(order, OrderStatus.Cancelled);
                break;
            case PaymentOutcome.Unknown:
                Advance(order, OrderStatus.PaymentUnknown);   // keep the hold, ask the provider later
                break;
        }
    }

    /// <summary>Money is taken. Make the order real, or give the money back. Never "confirm an expired hold".</summary>
    private void ConfirmPaid(Order order)
    {
        // Normal path: the hold is still alive.
        if (inventory.Confirm(order.Id)) { Confirmed(order); return; }

        // The hold expired while payment was unknown. Try a FRESH reservation.
        if (inventory.TryReserve(order.Id, order.Lines, HoldTtl) && inventory.Confirm(order.Id)) { Confirmed(order); return; }

        // No stock any more: refund.
        payments.Refund(PaymentKey(order));
        Advance(order, OrderStatus.Refunded);
    }

    private void Confirmed(Order order)
    {
        if (Advance(order, OrderStatus.Confirmed))
            OrderConfirmed?.Invoke(order);                 // `?.` = only if someone subscribed
    }

    /// <summary>The reconciliation job: for every order stuck in PaymentUnknown, ask the provider what really happened.</summary>
    public void Reconcile(Order order)
    {
        if (order.Status != OrderStatus.PaymentUnknown) return;

        switch (payments.Query(PaymentKey(order)))
        {
            case PaymentOutcome.Success:
                ConfirmPaid(order);
                break;
            case PaymentOutcome.Declined:
                inventory.Release(order.Id);
                Advance(order, OrderStatus.Cancelled);
                break;
            // Unknown again: leave it, the job will try on its next pass.
        }
    }

    private static string PaymentKey(Order o) => $"pay-{o.Id}";

    // Read the current version, then try the move with it. For one saga driving one order this is fine;
    // in a database the same thing is one statement:  WHERE id=? AND version=?
    private static bool Advance(Order o, OrderStatus to) => o.TryMove(to, o.Version);
}
