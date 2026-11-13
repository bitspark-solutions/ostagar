namespace Ostagar.Ledger;

/// <summary>
/// Which of the three lock tiers on a <see cref="FiscalPeriod"/> is being moved.
/// </summary>
/// <remarks>
/// The tiers are ordered by severity — soft excludes ordinary posters, hard excludes everyone writing
/// through the application, and tax marks the filing boundary. They exist as three fields on the period
/// row rather than one level column because a period can carry all three at once, each guarding a
/// different date.
/// </remarks>
public enum PeriodLockLevel
{
    /// <summary>Ordinary users may not post up to this date; the adviser tier still can.</summary>
    Soft,

    /// <summary>No posting through the application up to this date.</summary>
    Hard,

    /// <summary>The filing boundary: postings up to this date are refused because the return is filed.</summary>
    Tax,
}
