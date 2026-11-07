using Ostagar.Core;

namespace Ostagar.Modules.Partners;

/// <summary>
/// The credit check: exposure already outstanding, plus the order now proposed, against the limit granted.
/// </summary>
/// <remarks>
/// <para>
/// This is the application-side mirror of the constraint the database will also carry (invariant I8: limits
/// are constraints, not UI warnings). Checking here improves the message; it does not create the rule.
/// </para>
///
/// <para>
/// <b>Exact, with no tolerance and no rounding.</b> Comparison is on <see cref="decimal"/> at the currency's
/// own scale, so one paisa over the limit is refused and one paisa under is approved. The legacy validator's
/// ±0.01 credit tolerance — the same tolerance the ledger validator carried — meant an order could be accepted
/// at "within limit" on one screen and refused on the next, and a tenant's aged receivables quietly grew past
/// every limit they had set.
/// </para>
///
/// <para>
/// <b>The committed figure is exposure plus order, not order alone.</b> A factory that ships a second order to
/// a buyer who has not paid for the first is the whole reason a credit limit exists, and the exposure comes
/// from the AR projection — this module does not keep its own balance, because a second balance is a second
/// answer.
/// </para>
/// </remarks>
public static class CreditLimitRule
{
    /// <summary>
    /// Evaluates one proposed credit sale.
    /// </summary>
    /// <param name="creditLimit">The limit granted to the customer, or null when none has been granted.</param>
    /// <param name="currentExposure">What is already outstanding, including this order's own document if already raised.</param>
    /// <param name="proposedOrder">The value being added now.</param>
    /// <returns>A decision carrying the exact headroom or the exact overage.</returns>
    public static CreditDecision Evaluate(Money? creditLimit, Money currentExposure, Money proposedOrder)
    {
        if (creditLimit is not { } limit)
        {
            return CreditDecision.RefusedWithoutComparison(CreditDecisionStatus.CreditLimitNotSet, null);
        }

        if (currentExposure.Currency.Code != limit.Currency.Code || proposedOrder.Currency.Code != limit.Currency.Code)
        {
            return CreditDecision.RefusedWithoutComparison(CreditDecisionStatus.CurrencyMismatch, limit);
        }

        if (!proposedOrder.IsPositive)
        {
            return CreditDecision.RefusedWithoutComparison(CreditDecisionStatus.OrderValueInvalid, limit);
        }

        var committed = Money.Add(currentExposure, proposedOrder);

        if (committed > limit)
        {
            return new CreditDecision(CreditDecisionStatus.OverLimit, Money.Subtract(committed, limit), limit, committed);
        }

        return new CreditDecision(CreditDecisionStatus.Approved, Money.Subtract(limit, committed), limit, committed);
    }
}
