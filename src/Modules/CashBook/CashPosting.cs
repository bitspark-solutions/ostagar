using Garments.Core;
using Garments.Ledger;

namespace Garments.Modules.CashBook;

/// <summary>
/// Builds the ledger request one cash document becomes.
/// </summary>
/// <remarks>
/// <para>
/// The mapping is deliberately small, and smallness is the point: a receipt debits the cash account
/// and credits the counter account; a payment is the same picture turned around. Every rule beyond
/// that — balance, period locks, account postability, gapless numbering — is the ledger's, reached
/// through <see cref="IPoster"/>, so the cash book cannot drift into a second, weaker posting engine
/// of its own. The legacy path skipped this layer entirely and inserted a single-sided
/// <c>JournalEntry</c> marked <c>Posted</c>, which is the precise defect this shape exists to prevent.
/// </para>
///
/// <para>
/// The journal is chosen by the cash account's declared subtype: cash to <c>CASH</c>, bank to
/// <c>BANK</c>, anything else refused. Two journals rather than a flag on one, because each owns a
/// gapless number sequence and a factory owner asks "where is CASH-000041" on paper.
/// </para>
/// </remarks>
public static class CashPosting
{
    /// <summary>
    /// Plans the posting for a recorded transaction. The result carries either a request ready for
    /// <see cref="IPoster"/> or every reason the plan was refused.
    /// </summary>
    /// <param name="transaction">The recorded document. Its identity becomes the posting's source key,
    /// so a retry of this call cannot double-post.</param>
    /// <param name="cashAccountSubType">The cash account's subtype, resolved by the caller from the
    /// chart of accounts — passed in rather than queried so this builder stays free of persistence.</param>
    public static CashPostingPlan For(CashTransaction transaction, AccountSubType cashAccountSubType)
    {
        var journalCode = cashAccountSubType switch
        {
            AccountSubType.Cash => StandardJournals.CashCode,
            AccountSubType.Bank => StandardJournals.BankCode,
            _ => null,
        };

        if (journalCode is null)
        {
            return CashPostingPlan.Refused(new CashTransactionError(
                CashTransactionErrorCode.CashAccountNotCashLike,
                "The cash side of the transaction must be a cash or bank account."));
        }

        var amount = transaction.TypedAmount();

        var cashLine = new PostingLine
        {
            AccountId = transaction.CashAccountId,
            Debit = transaction.Kind == CashTransactionKind.Receipt ? amount : null,
            Credit = transaction.Kind == CashTransactionKind.Payment ? amount : null,
            AmountInCurrency = amount.Amount,
            Currency = transaction.Currency,
            Memo = transaction.Memo,
        };

        var counterLine = new PostingLine
        {
            AccountId = transaction.CounterAccountId,
            Debit = transaction.Kind == CashTransactionKind.Payment ? amount : null,
            Credit = transaction.Kind == CashTransactionKind.Receipt ? amount : null,
            AmountInCurrency = amount.Amount,
            Currency = transaction.Currency,
            Memo = transaction.Memo,
        };

        return CashPostingPlan.Ready(new PostingRequest
        {
            JournalCode = journalCode,
            PostingDate = transaction.TransactionDate,
            SourceType = CashSourceTypes.Of(transaction.Kind),
            SourceId = transaction.Id,
            Iteration = transaction.Iteration,
            ActorUserId = transaction.ActorUserId,
            Memo = transaction.Memo,
            Lines = [cashLine, counterLine],
        });
    }
}
