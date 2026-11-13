using Ostagar.Core;

namespace Ostagar.Modules.CashBook;

/// <summary>
/// Turns a parsed side of a cash-book row into a <see cref="NewCashTransaction"/>.
/// </summary>
/// <remarks>
/// The category → account mapping is handed in by the caller, resolved from the tenant's own chart.
/// It is an input rather than a lookup this type performs so the module stays free of persistence —
/// and so the one thing the importer must never do is easy to see it not doing: the legacy import
/// auto-created Chart-of-Accounts rows from free-text categories with randomly generated codes, and
/// the rebuilt gate is explicit-mapping-or-refusal. The refusal is <see
/// cref="CashBookImportErrorCode.CategoryUnmapped"/> and it names the category.
/// </remarks>
public static class CashBookImportMapper
{
    /// <summary>
    /// Maps one side. <paramref name="accountsByCategory"/> keys are compared trimmed and
    /// case-insensitively, because the book's categories are hand-typed and "SubContract Bill" and
    /// "Subcontract bill" are the same account.
    /// </summary>
    public static CashBookImportDraft Map(
        CashBookImportSide side,
        Guid cashAccountId,
        Currency currency,
        IReadOnlyDictionary<string, Guid> accountsByCategory)
    {
        if (!accountsByCategory.TryGetValue(side.Category, out var counterAccountId))
        {
            return CashBookImportDraft.Refused(new CashBookImportError(
                side.LineNumber, side.Kind, CashBookImportErrorCode.CategoryUnmapped,
                $"The category '{side.Category}' is not in the import's account mapping."));
        }

        return CashBookImportDraft.Ready(new NewCashTransaction
        {
            Kind = side.Kind,
            TransactionDate = side.TransactionDate,
            CashAccountId = cashAccountId,
            CounterAccountId = counterAccountId,
            Amount = Money.Round(currency, side.Amount),
            Memo = Memo(side),
        });
    }

    /// <summary>
    /// The memo the transaction carries: the particulars column, with the payment side's supplier and
    /// buyer appended so the book's extra columns survive the move into the ledger.
    /// </summary>
    private static string Memo(CashBookImportSide side)
    {
        var memo = side.Particulars;

        if (side.Supplier.Length > 0)
        {
            memo = Join(memo, $"supplier: {side.Supplier}");
        }

        if (side.Buyer.Length > 0)
        {
            memo = Join(memo, $"buyer: {side.Buyer}");
        }

        return memo;
    }

    private static string Join(string memo, string extra) =>
        memo.Length == 0 ? extra : memo + " | " + extra;
}
