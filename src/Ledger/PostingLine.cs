using Ostagar.Core;

namespace Ostagar.Ledger;

/// <summary>
/// A line proposed for posting, before anything is written.
/// </summary>
/// <remarks>
/// Debit and credit are mutually exclusive and typed as <see cref="Money"/>, so a line cannot name two
/// currencies or carry an amount that has quietly lost its scale. The database enforces the same shape
/// with a CHECK; this is the version that produces a readable error for a human instead of a
/// constraint violation from Postgres.
/// </remarks>
public sealed record PostingLine
{
    public required Guid AccountId { get; init; }

    /// <summary>Functional-currency debit. Exactly one of this and <see cref="Credit"/> must be set.</summary>
    public Money? Debit { get; init; }

    /// <summary>Functional-currency credit. Exactly one of this and <see cref="Debit"/> must be set.</summary>
    public Money? Credit { get; init; }

    /// <summary>The amount as the source document stated it, in that document's currency.</summary>
    public decimal AmountInCurrency { get; init; }

    public CurrencyCode Currency { get; init; } = CurrencyCode.Bdt;

    /// <summary>
    /// Functional currency per one unit of <see cref="Currency"/>. Null means the posting engine should
    /// resolve it from the company's rate history for the line's date.
    /// </summary>
    /// <remarks>
    /// Null rather than a default of <c>1m</c>. A default is indistinguishable from "this dollar line is
    /// worth one taka", and it silently prices every foreign-currency line at par unless somebody
    /// remembers to set it — which is the same failure as a currency field that defaults to USD, arrived
    /// at from the numeric side.
    /// </remarks>
    public decimal? ExchangeRate { get; init; }

    public DateOnly ExchangeRateDate { get; init; }

    public Guid? AnalysisId { get; init; }

    public string Memo { get; init; } = string.Empty;

    /// <summary>
    /// Which side this line falls on. A line with both sides or neither reports debit, and both shapes are
    /// refused by <see cref="PostingBalance"/> before this value is used for anything.
    /// </summary>
    public bool IsDebit => Debit is not null;
}
