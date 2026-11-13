namespace Ostagar.Modules.CashBook;

/// <summary>
/// One half of a row in the client's two-sided cash book, parsed but not yet mapped to accounts.
/// </summary>
/// <remarks>
/// The client's paper book — and the CSV exported from it — prints receipts down the left half of the
/// page and payments down the right, so a single physical line can carry a receipt, a payment, both,
/// or (for blank filler rows) neither. Each present half is one independent transaction in the ledger;
/// nothing on the line links them beyond sharing a row.
/// </remarks>
public sealed record CashBookImportSide
{
    /// <summary><see cref="CashTransactionKind.Receipt"/> for the left (money-in) half,
    /// <see cref="CashTransactionKind.Payment"/> for the right (money-out) half.</summary>
    public required CashTransactionKind Kind { get; init; }

    /// <summary>One-based line number in the file, for error reporting and the idempotency key.</summary>
    public required int LineNumber { get; init; }

    public required DateOnly TransactionDate { get; init; }

    /// <summary>
    /// The book's own label for what the movement was — "Fabric- Purchase", "Received: Urbo ltd".
    /// Resolved to a ledger account by the caller's explicit mapping, never auto-created.
    /// </summary>
    public required string Category { get; init; }

    /// <summary>The amount as printed, already positive: the side of the page carries the sign.</summary>
    public required decimal Amount { get; init; }

    /// <summary>Free-text particulars column; becomes the transaction memo.</summary>
    public string Particulars { get; init; } = string.Empty;

    /// <summary>Debit side only: the supplier column. Empty on receipts.</summary>
    public string Supplier { get; init; } = string.Empty;

    /// <summary>Debit side only: the buyer column. Empty on receipts.</summary>
    public string Buyer { get; init; } = string.Empty;
}
