namespace Ostagar.Modules.CashBook;

/// <summary>
/// One import refusal, tied to the physical line and the side of the page it came from.
/// </summary>
/// <param name="LineNumber">One-based line number in the uploaded file, so a clerk can find the row
/// in Excel rather than in a database.</param>
/// <param name="Kind">Which half of the two-sided row failed, when the error belongs to a side.
/// Null for file- and row-level problems (a missing header, a short row).</param>
/// <param name="Code">The machine-readable reason.</param>
/// <param name="Message">The human-readable reason, English; the client translates from
/// <paramref name="Code"/>.</param>
public sealed record CashBookImportError(
    int LineNumber,
    CashTransactionKind? Kind,
    CashBookImportErrorCode Code,
    string Message);
