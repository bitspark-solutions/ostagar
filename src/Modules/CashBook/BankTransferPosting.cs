using Ostagar.Core;
using Ostagar.Ledger;

namespace Ostagar.Modules.CashBook;

public static class BankTransferPosting
{
    public static PostingRequest Build(Guid id, BankAccount from, BankAccount to, DateOnly date,
        Money amount, Money functionalAmount, Guid actor, string memo)
    {
        if (id == Guid.Empty || actor == Guid.Empty)
            throw new BankingRuleException(BankingErrorCode.InvalidIdentity);
        if (from.Id == to.Id || from.LedgerAccountId == to.LedgerAccountId)
            throw new BankingRuleException(BankingErrorCode.SameAccount);
        if (from.Currency != to.Currency || from.Currency != amount.Currency.Code)
            throw new BankingRuleException(BankingErrorCode.CurrencyMismatch);
        if (amount.Amount <= decimal.Zero || functionalAmount.Amount <= decimal.Zero)
            throw new BankingRuleException(BankingErrorCode.AmountNotPositive);
        if (memo.Length > BankingVocabulary.MemoLength)
            throw new BankingRuleException(BankingErrorCode.InvalidMemo);
        return new PostingRequest
        {
            JournalCode = StandardJournals.BankCode, SourceType = BankingVocabulary.TransferSource,
            SourceId = id, PostingDate = date, ActorUserId = actor, Memo = memo,
            Lines =
            [
                new PostingLine { AccountId = from.LedgerAccountId, Credit = functionalAmount,
                    Currency = amount.Currency.Code, AmountInCurrency = amount.Amount, Memo = memo },
                new PostingLine { AccountId = to.LedgerAccountId, Debit = functionalAmount,
                    Currency = amount.Currency.Code, AmountInCurrency = amount.Amount, Memo = memo },
            ],
        };
    }
}
