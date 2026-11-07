namespace Garments.Modules.CashBook;

/// <summary>
/// Which way the money moved. The cash book is a two-column book in the the client format its users
/// already keep on paper — receipts on one side, payments on the other — and the kind is what decides
/// which account the posting debits.
/// </summary>
public enum CashTransactionKind
{
    /// <summary>Money came in: debit the cash account, credit the counter account.</summary>
    Receipt,

    /// <summary>Money went out: debit the counter account, credit the cash account.</summary>
    Payment,
}
