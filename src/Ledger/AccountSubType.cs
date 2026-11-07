namespace Ostagar.Ledger;

/// <summary>
/// What an account is <i>for</i>, which is what decides where it appears on a statement.
/// </summary>
/// <remarks>
/// The legacy tree derived this by string matching on account names —
/// <c>accountNameLower.Contains("cash")</c> — inside the trial balance service. That means renaming an
/// account moves money between lines of the balance sheet, and a tenant who names an account
/// "Cash Discount Received" gets it classified as an asset.
///
/// It is a column here, and it is <b>immutable once the account has been posted to</b>: the old code
/// let <c>AccountType</c> be changed on a live account, which silently inverted the sign of every
/// historical balance for that account while leaving the ledger untouched. Reports that had been
/// correct the week before stopped being correct, with nothing in the data to explain it.
/// </remarks>
public enum AccountSubType
{
    // Assets
    Cash,
    Bank,
    Receivable,
    Inventory,
    FixedAsset,
    Prepayment,

    // Liabilities
    Payable,
    TaxPayable,
    Loan,
    Accrual,
    DeferredIncome,

    // Equity
    OwnersCapital,
    RetainedEarnings,
    Drawings,

    // Income
    SalesRevenue,
    ServiceRevenue,
    OtherIncome,

    // Expenses
    CostOfGoodsSold,
    PayrollExpense,
    RentExpense,
    UtilityExpense,
    OperatingExpense,
    FinanceCost,
}
