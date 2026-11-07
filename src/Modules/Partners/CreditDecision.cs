using Ostagar.Core;

namespace Ostagar.Modules.Partners;

/// <summary>
/// The outcome of a credit check, carrying the exact amount by which the order fits or does not.
/// </summary>
/// <remarks>
/// There is one <see cref="Amount"/> and its meaning is fixed by <see cref="Status"/>: remaining headroom
/// when approved, overage when refused. Two separate nullable fields would invite a caller to read the wrong
/// one; a single field cannot be misread, and both cases are non-negative by construction of the check rather
/// than by clamping a negative to zero.
///
/// The difference is never rounded or suppressed. A refusal that reports "over by BDT 0.01" is a real refusal
/// with a real repair — drop the order by one paisa, or raise the limit by one paisa — and an approval that
/// reports "0.00 headroom left" is the boundary case an owner will ask about, so it is stated exactly.
/// </remarks>
public sealed record CreditDecision
{
    public CreditDecision(CreditDecisionStatus status, Money? amount, Money? limit, Money? committed)
    {
        Status = status;
        Amount = amount;
        Limit = limit;
        Committed = committed;
    }

    public CreditDecisionStatus Status { get; }

    /// <summary>Exact headroom or exact overage, in the limit's currency. Null only when the check never got as far as a comparison.</summary>
    public Money? Amount { get; }

    /// <summary>The partner's credit limit, or null when none is granted.</summary>
    public Money? Limit { get; }

    /// <summary>Existing exposure plus the proposed order, as at the check date.</summary>
    public Money? Committed { get; }

    public bool IsApproved => Status == CreditDecisionStatus.Approved;

    /// <summary>A refusal that never reached a comparison, so it has no figures to show.</summary>
    public static CreditDecision RefusedWithoutComparison(CreditDecisionStatus status, Money? limit) =>
        new(status, null, limit, null);
}
