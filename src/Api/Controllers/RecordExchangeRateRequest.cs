namespace Ostagar.Api.Controllers;

/// <summary>
/// Payload for appending one quote to the company's rate history.
/// </summary>
/// <param name="Currency">ISO currency code the quote is for. The functional currency itself is
/// refused — its rate is 1 by definition, and recording one reopens the question the chart answers.</param>
/// <param name="ValidFrom">The first posting date the quote applies to. Rates are append-only and
/// effective-dated: the same currency at the same start date is refused, so corrections are new
/// entries with a later <paramref name="ValidFrom"/>, never edits.</param>
/// <param name="Rate">Functional units per one unit of the currency (for BDT books: taka per dollar).</param>
/// <param name="Source">Where the quote came from — a Bangladesh Bank circular, a bank letter, a
/// treasury feed. A rate with no source is unfalsifiable, so it is required.</param>
public sealed record RecordExchangeRateRequest(
    string Currency,
    DateOnly ValidFrom,
    decimal Rate,
    string Source);
