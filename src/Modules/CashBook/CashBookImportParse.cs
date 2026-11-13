namespace Ostagar.Modules.CashBook;

/// <summary>
/// What <see cref="CashBookImportParser.Parse"/> read from a file: the sides that parsed, and every
/// reason the rest did not.
/// </summary>
/// <remarks>
/// Sides and errors are both returned rather than failing the file on the first bad row. A cash-book
/// export is typed by hand and a wrong date on line 40 does not make the receipt on line 3 less real —
/// the gate is that each imported row posts balanced through the ledger, not that the file is perfect.
/// </remarks>
public sealed record CashBookImportParse
{
    /// <summary>Every side of every row that parsed cleanly, in file order.</summary>
    public IReadOnlyList<CashBookImportSide> Sides { get; init; } = [];

    /// <summary>Every line-level and file-level problem found, in file order.</summary>
    public IReadOnlyList<CashBookImportError> Errors { get; init; } = [];
}
