using Ostagar.Core;

namespace Ostagar.Ledger;

/// <summary>
/// The outcome of checking a proposed posting.
/// </summary>
/// <param name="Succeeded">True only when every rule passed.</param>
/// <param name="Errors">Every rule that failed. All failures are reported at once rather than the first,
/// because a person fixing a journal should not have to save four times to learn four things.</param>
/// <param name="TotalDebit">Functional-currency debit total.</param>
/// <param name="TotalCredit">Functional-currency credit total.</param>
public sealed record PostingBalanceResult(
    bool Succeeded,
    IReadOnlyList<PostingError> Errors,
    decimal TotalDebit,
    decimal TotalCredit)
{
    /// <summary>Foreign-currency totals by currency code, for the per-currency balance check and for FX reporting.</summary>
    public IReadOnlyDictionary<CurrencyCode, (decimal Debit, decimal Credit)> CurrencyTotals { get; init; }
        = new Dictionary<CurrencyCode, (decimal, decimal)>();

    /// <summary>What must be posted to a rounding account to make the entry balance, or zero.</summary>
    public decimal RoundingResidual { get; init; }
}
