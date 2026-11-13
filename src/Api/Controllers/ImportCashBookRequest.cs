namespace Ostagar.Api.Controllers;

/// <summary>
/// What a client submits to import one cash-book CSV file.
/// </summary>
/// <remarks>
/// JSON rather than multipart, matching every other write endpoint in this surface: the file is the
/// CSV text itself in <see cref="Content"/>. There is nothing else in the envelope a form encoding
/// would carry better, and one content type for all writes keeps the tests and the frontend honest.
/// </remarks>
public sealed record ImportCashBookRequest
{
    /// <summary>The raw CSV text, in the client's two-sided cash-book layout.</summary>
    public required string Content { get; init; }

    /// <summary>The name the file had, for the audit trail. Not parsed for anything.</summary>
    public string? FileName { get; init; }

    /// <summary>The company's own cash or bank account every imported row moved through.</summary>
    public required Guid CashAccountId { get; init; }

    /// <summary>
    /// ISO code of the currency the book is kept in. Null means the company's functional currency —
    /// the client's book is a taka book, and this exists for the same reason it does on single
    /// transactions.
    /// </summary>
    public string? Currency { get; init; }

    /// <summary>
    /// The explicit category → account-code mapping: keys are the book's category strings
    /// ("Fabric- Purchase"), values are codes in the company's own chart ("5010").
    /// </summary>
    /// <remarks>
    /// Explicit, and required for any category that appears. The predecessor auto-created CoA accounts
    /// from free text with <c>new Random().Next(100,999)</c> codes; the P3 gate replaces that with a
    /// mapping the importer states, so an unmapped category is a refused row with a reason, not a new
    /// account nobody chose.
    /// </remarks>
    public required IReadOnlyDictionary<string, string> CategoryAccountCodes { get; init; }
}
