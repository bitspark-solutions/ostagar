namespace Ostagar.Modules.CashBook;

public sealed record BankStatementReport(string Currency, DateOnly AsOf, decimal? StatementClosingBalance,
    IReadOnlyList<BankStatementLine> Rows)
{
    public decimal LedgerBalance => Rows.Sum(x => x.Amount);
    public decimal OutstandingMovement => Rows.Where(x => !x.Reconciled).Sum(x => x.Amount);
    public decimal? AdjustedStatementBalance => StatementClosingBalance + OutstandingMovement;
    public decimal? Difference => LedgerBalance - AdjustedStatementBalance;
}
