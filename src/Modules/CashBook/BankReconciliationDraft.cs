namespace Ostagar.Modules.CashBook;

public sealed record BankReconciliationDraft(Guid Id, DateOnly From, DateOnly To,
    decimal OpeningBalance, decimal ClosingBalance, IReadOnlyList<Guid> GlLineIds);
