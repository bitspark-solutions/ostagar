namespace Ostagar.Api.Controllers;

/// <summary>
/// One recorded quote from the company's effective-dated rate history.
/// </summary>
/// <param name="Id">Row identifier.</param>
/// <param name="Currency">The currency the quote prices in functional units.</param>
/// <param name="ValidFrom">First posting date the quote applies to.</param>
/// <param name="Rate">Functional units per one unit of the currency.</param>
/// <param name="Source">Where the quote was taken from.</param>
/// <param name="RecordedAt">When the quote was recorded.</param>
/// <param name="RecordedByUserId">Who recorded it, when known.</param>
public sealed record ExchangeRateResponse(
    Guid Id,
    string Currency,
    DateOnly ValidFrom,
    decimal Rate,
    string Source,
    DateTimeOffset RecordedAt,
    Guid? RecordedByUserId);
