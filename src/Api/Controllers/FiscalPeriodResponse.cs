namespace Ostagar.Api.Controllers;

/// <summary>
/// One fiscal period with its lock state, as the closing calendar shows it.
/// </summary>
/// <param name="Id">Period identifier.</param>
/// <param name="Number">Period number within the fiscal year, 1–12 from the year's start month.</param>
/// <param name="FiscalYear">The fiscal year the period belongs to.</param>
/// <param name="Name">English label.</param>
/// <param name="NameBangla">Bangla label for printed reports.</param>
/// <param name="StartDate">First day of the period.</param>
/// <param name="EndDate">Last day of the period.</param>
/// <param name="IsClosed">Closed flag carried on the row.</param>
/// <param name="SoftLockedOn">Postings up to and including this date need the adviser tier.</param>
/// <param name="HardLockedOn">Postings up to and including this date are refused to everyone.</param>
/// <param name="TaxLockedOn">Postings up to and including this date are refused as filed.</param>
public sealed record FiscalPeriodResponse(
    Guid Id,
    int Number,
    int FiscalYear,
    string Name,
    string NameBangla,
    DateOnly StartDate,
    DateOnly EndDate,
    bool IsClosed,
    DateOnly? SoftLockedOn,
    DateOnly? HardLockedOn,
    DateOnly? TaxLockedOn);
