namespace Garments.Modules.CashBook;

/// <summary>
/// The cash book's own rules, checked before the ledger is asked anything.
/// </summary>
/// <remarks>
/// Only rules that belong to the document live here — an amount, two distinct accounts, a known
/// direction. Whether the accounts exist, are postable, or sit inside an open period is the ledger's
/// answer and arrives through the posting outcome, so those checks appear exactly once in the
/// codebase rather than once per posting source.
/// </remarks>
public static class CashTransactionValidator
{
    public static CashTransactionDecision Validate(NewCashTransaction transaction)
    {
        var errors = new List<CashTransactionError>();

        if (transaction.CashAccountId == Guid.Empty || transaction.CounterAccountId == Guid.Empty)
        {
            errors.Add(new CashTransactionError(
                CashTransactionErrorCode.AccountMissing,
                "A cash transaction needs both the cash account and the account the movement is for."));
        }
        else if (transaction.CashAccountId == transaction.CounterAccountId)
        {
            errors.Add(new CashTransactionError(
                CashTransactionErrorCode.SameAccountBothSides,
                "The cash account and the counter account are the same account."));
        }

        if (transaction.Amount is null)
        {
            errors.Add(new CashTransactionError(
                CashTransactionErrorCode.AmountMissing, "An amount is required."));
        }
        else if (!transaction.Amount.Value.IsPositive)
        {
            errors.Add(new CashTransactionError(
                CashTransactionErrorCode.AmountNotPositive,
                "The amount must be positive; the direction is already stated by the transaction kind."));
        }

        return errors.Count == 0
            ? CashTransactionDecision.Ok()
            : CashTransactionDecision.Refused([.. errors]);
    }

    /// <summary>
    /// Whether a recorded document may be corrected. Reversal is the only edit a posted cash line
    /// accepts, and it applies exactly once.
    /// </summary>
    public static CashTransactionDecision CanReverse(CashTransaction transaction)
    {
        if (transaction.PostedEntryId is null)
        {
            return CashTransactionDecision.Refused(new CashTransactionError(
                CashTransactionErrorCode.TransactionNotPosted,
                "The transaction has no posted entry to reverse."));
        }

        if (transaction.Status == CashTransactionStatus.Reversed || transaction.ReversedByEntryId is not null)
        {
            return CashTransactionDecision.Refused(new CashTransactionError(
                CashTransactionErrorCode.TransactionAlreadyReversed,
                "The transaction has already been reversed."));
        }

        return CashTransactionDecision.Ok();
    }
}
