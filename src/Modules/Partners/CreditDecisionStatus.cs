namespace Ostagar.Modules.Partners;

/// <summary>
/// How a credit check ended. Four answers, because a caller has to be able to tell "refused" from "refused
/// because nobody ever set a limit" from "refused because the figures are not comparable".
/// </summary>
public enum CreditDecisionStatus
{
    /// <summary>The committed exposure is within the limit. Equality is approved: a limit is inclusive.</summary>
    Approved,

    /// <summary>The committed exposure is over the limit by a stated, exact amount.</summary>
    OverLimit,

    /// <summary>
    /// No limit has been granted. Refused rather than treated as unlimited — an absent decision is not a
    /// permissive one, and this is the state a partner imported without a credit review lands in.
    /// </summary>
    CreditLimitNotSet,

    /// <summary>Exposure, limit and order do not name the same currency, so no comparison exists.</summary>
    CurrencyMismatch,

    /// <summary>The proposed order value is not a sale: zero or negative.</summary>
    OrderValueInvalid,
}
