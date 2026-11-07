namespace Ostagar.Ledger;

/// <summary>
/// Derives the normal balance from the account type, in one place.
/// </summary>
/// <remarks>
/// The legacy build had this logic in three different services and they disagreed:
/// <c>TrialBalanceCalculationService</c> used a debits-negative convention and defined
/// <c>FinalBalance = credits − debits</c>, which is zero for any balanced ledger and therefore
/// meaningless; <c>TrialBalanceService</c> returned debits as negative numbers; and
/// <c>BalanceService</c> used normal-balance signs. Three screens, three answers, one database.
///
/// It is a pure function of <see cref="AccountType"/> here, so there is exactly one answer and it
/// cannot drift from the data.
/// </remarks>
public static class NormalBalanceOf
{
    public static NormalBalance For(AccountType type) => type switch
    {
        AccountType.Asset or AccountType.Expense => NormalBalance.Debit,
        AccountType.Liability or AccountType.Equity or AccountType.Income => NormalBalance.Credit,
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown account type."),
    };

    /// <summary>
    /// Turns debit and credit totals into a signed balance on the account's own terms: positive means
    /// the account holds its normal balance, negative means it is on the opposite side.
    /// </summary>
    /// <remarks>
    /// A bank overdraft and a contra-asset are genuinely negative-normal accounts and must render as
    /// negative. The legacy grid wrapped every figure in <c>Math.abs()</c>, which made a credit balance
    /// of −40,000 display as <c>$40,000.00</c> — so a reader could not distinguish debit from credit at
    /// all, in the one column whose header promised that distinction.
    /// </remarks>
    public static decimal Balance(decimal debitTotal, decimal creditTotal, AccountType type) =>
        For(type) == NormalBalance.Debit
            ? debitTotal - creditTotal
            : creditTotal - debitTotal;
}
