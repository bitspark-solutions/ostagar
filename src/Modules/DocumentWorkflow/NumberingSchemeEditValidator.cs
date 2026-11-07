using System.Globalization;

namespace Ostagar.Modules.DocumentWorkflow;

/// <summary>
/// Decides what a tenant is allowed to change on a numbering scheme that has already issued numbers.
/// </summary>
/// <remarks>
/// <para>
/// The fields that decide a number's <em>identity</em> — prefix, padding width, period component, fiscal
/// start month, starting value — are frozen the moment the scheme issues anything. The fields that only
/// describe it (the settings-screen name in both languages) stay editable. Everything else about a
/// scheme can change, but these cannot, because documents already in circulation carry a string built
/// from them and an auditor is entitled to hold that string against this rule forever.
/// </para>
/// <para>
/// <b>Why a narrower width is refused outright.</b> Padding is the only thing keeping a series the same
/// length, and there are exactly three responses to a value that outgrows a reduced width. Truncate:
/// the value 12 renders as <c>12</c> where <c>12</c> was already issued as <c>0012</c>, so the tenant has
/// built two documents with one number — the failure this rule exists to prevent. Grow silently: the
/// series now mixes lengths, and ordering it as text puts <c>SO-9</c> after <c>SO-10</c>, which is the
/// legacy voucher bug and reads to an auditor as a gap. Refuse: the only honest option, and the one
/// taken here.
/// </para>
/// <para>
/// <b>Why a wider width is allowed.</b> Widening can never re-render an issued value into a string that
/// already exists, because values only ever move forward. It costs a consistent-looking series, which is
/// cosmetic, and the printed length of a number is allowed to grow when a factory outgrows four digits.
/// </para>
/// </remarks>
public static class NumberingSchemeEditValidator
{
    /// <summary>
    /// Compares the stored scheme to the proposed one against the counters that already exist.
    /// </summary>
    /// <param name="current">The scheme as stored.</param>
    /// <param name="proposed">What the tenant is asking to save. A shape-invalid proposal is refused here
    /// too, so a settings screen cannot persist a scheme the allocator would later reject.</param>
    /// <param name="sequences">Every counter row belonging to the scheme, including closed periods. A
    /// closed period still counts: its numbers are on paper.</param>
    public static NumberingSchemeEditDecision Validate(
        DocumentNumberingScheme current,
        DocumentNumberingScheme proposed,
        IReadOnlyList<NumberingSequenceState> sequences)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(proposed);
        ArgumentNullException.ThrowIfNull(sequences);

        var errors = new List<NumberingError>(NumberingSchemeRules.ValidateShape(proposed));

        var highestIssuedValue = 0;
        var issuedAnything = false;

        foreach (var sequence in sequences)
        {
            if (sequence.SchemeId != current.Id)
            {
                continue;
            }

            if (sequence.IssuedCount > 0)
            {
                issuedAnything = true;
            }

            // The counter itself, not just the values handed out: a scheme widened below a counter that
            // has climbed to five digits would be refused at the next allocation anyway, and a tenant
            // deserves to hear that at the moment they edit rather than when a clerk prints an order.
            highestIssuedValue = Math.Max(highestIssuedValue, Math.Max(sequence.NextValue - 1, 0));
        }

        if (issuedAnything)
        {
            if (!string.Equals(current.Prefix, proposed.Prefix, StringComparison.Ordinal))
            {
                errors.Add(new NumberingError(
                    NumberingErrorCode.PrefixChangedAfterIssue,
                    "Issued documents already carry this prefix on paper; changing it would move their numbers into a series an auditor cannot find. Add a new scheme instead."));
            }

            if (proposed.Width < current.Width)
            {
                errors.Add(new NumberingError(
                    NumberingErrorCode.WidthNarrowedAfterIssue,
                    $"The width cannot go from {current.Width.ToString(CultureInfo.InvariantCulture)} to {proposed.Width.ToString(CultureInfo.InvariantCulture)} once numbers are in circulation: either the new form collides with a number already issued or the series stops being orderable as text."));
            }

            if (current.PeriodComponent != proposed.PeriodComponent)
            {
                errors.Add(new NumberingError(
                    NumberingErrorCode.PeriodComponentChangedAfterIssue,
                    "Changing the period component re-opens spans that are already closed, and a re-opened span re-issues numbers that are on paper."));
            }

            if (current.StartingValue != proposed.StartingValue)
            {
                errors.Add(new NumberingError(
                    NumberingErrorCode.StartingValueChangedAfterIssue,
                    "A starting value below a counter that has already passed it re-issues allocated numbers, so this scheme's first series is fixed."));
            }

            if (current.FiscalStartMonth != proposed.FiscalStartMonth)
            {
                errors.Add(new NumberingError(
                    NumberingErrorCode.FiscalStartMonthChangedAfterIssue,
                    "Re-cutting the fiscal year moves issued documents into different periods, which is a numbering change, not a calendar preference."));
            }
        }

        if (proposed.Width < NumberingFormat.DigitsOf(highestIssuedValue))
        {
            errors.Add(new NumberingError(
                NumberingErrorCode.WidthNarrowerThanCounter,
                $"Width {proposed.Width.ToString(CultureInfo.InvariantCulture)} cannot render the counter value {highestIssuedValue.ToString(CultureInfo.InvariantCulture)} this scheme has already reached."));
        }

        return new NumberingSchemeEditDecision(errors.Count == 0, errors);
    }
}
