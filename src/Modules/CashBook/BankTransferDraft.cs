namespace Ostagar.Modules.CashBook;

public sealed record BankTransferDraft(Guid Id, Guid FromBankAccountId, Guid ToBankAccountId,
    DateOnly PostingDate, decimal Amount, string Memo);
