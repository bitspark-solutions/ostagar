namespace Ostagar.Modules.CashBook;

public sealed record BankStatementLine(Guid Id, Guid EntryId, string Number, DateOnly PostingDate,
    string Memo, decimal Amount, bool Reconciled);
