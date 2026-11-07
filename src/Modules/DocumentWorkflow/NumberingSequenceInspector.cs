namespace Ostagar.Modules.DocumentWorkflow;

/// <summary>
/// Proves a period's numbering series is whole, from the numbers on the documents alone.
/// </summary>
/// <remarks>
/// The allocator guarantees gaplessness going forward; this is what makes the guarantee checkable after
/// the fact, which is the difference between a rule and an assertion. It is the same idea as the
/// <c>Verifier</c> stage of the plan: a projection anyone can re-derive from the stored facts.
///
/// Cancelled documents must be in the list. Their numbers were consumed and belong in the series, and a
/// report that silently skipped them would report the absence of a cancelled document as a gap — which is
/// precisely the confusion between "deleted" and "never issued" that the no-gap rule exists to forbid.
///
/// A value below the scheme's starting value is not treated as a gap. Nothing in this module can move a
/// counter backwards — <see cref="NumberingErrorCode.CounterBelowStartingValue"/> refuses even that — so
/// a row carrying such a number was written by direct database access, which is a storage finding rather
/// than a numbering one.
/// </remarks>
public static class NumberingSequenceInspector
{
    private static readonly IReadOnlyList<NumberingValueRange> NoRanges = [];
    private static readonly IReadOnlyList<string> NoStrings = [];

    /// <summary>
    /// Reads every number issued for one scheme in one period and reports what is missing, doubled or
    /// unexplainable.
    /// </summary>
    /// <param name="scheme">The scheme that rendered them, so the shapes can be checked rather than assumed.</param>
    /// <param name="period">The period all the numbers must belong to; a report that mixes two periods
    /// would call a legitimate restart a gap.</param>
    /// <param name="issuedNumbers">The stored number of each document in the period, cancelled ones
    /// included. A null or blank entry is reported as unexplainable, which is the printed form of a
    /// document that was never registered.</param>
    public static NumberingGapReport Inspect(
        DocumentNumberingScheme scheme,
        NumberingPeriodKey period,
        IReadOnlyList<string?> issuedNumbers)
    {
        ArgumentNullException.ThrowIfNull(scheme);
        ArgumentNullException.ThrowIfNull(issuedNumbers);

        var shapeErrors = NumberingSchemeRules.ValidateShape(scheme);

        if (shapeErrors.Count > 0)
        {
            throw new ArgumentException(
                $"The scheme is not a valid basis for an audit: {shapeErrors[0].Message}",
                nameof(scheme));
        }

        var unreadable = new List<string>();
        var holders = new Dictionary<int, List<string?>>();

        foreach (var number in issuedNumbers)
        {
            if (NumberingFormat.TryParseValue(scheme.Prefix, period, scheme.Width, number, out var value))
            {
                if (!holders.TryGetValue(value, out var existing))
                {
                    existing = [];
                    holders[value] = existing;
                }

                existing.Add(number);
                continue;
            }

            unreadable.Add(string.IsNullOrWhiteSpace(number) ? "(no number)" : number!);
        }

        if (holders.Count == 0)
        {
            return new NumberingGapReport(
                unreadable.Count == 0,
                NoRanges,
                NoStrings,
                unreadable,
                0,
                0);
        }

        var values = holders.Keys.ToArray();
        Array.Sort(values);

        var lowest = values[0];
        var highest = values[^1];

        var missing = new List<NumberingValueRange>();

        if (lowest > scheme.StartingValue)
        {
            missing.Add(new NumberingValueRange(scheme.StartingValue, lowest - 1));
        }

        for (var index = 1; index < values.Length; index++)
        {
            var gapFrom = values[index - 1] + 1;
            var gapTo = values[index] - 1;

            if (gapFrom <= gapTo)
            {
                missing.Add(new NumberingValueRange(gapFrom, gapTo));
            }
        }

        var duplicates = new List<string>();

        foreach (var value in values)
        {
            if (holders[value].Count > 1)
            {
                duplicates.Add(
                    NumberingFormat.Render(scheme.Prefix, period, scheme.Width, value));
            }
        }

        return new NumberingGapReport(
            missing.Count == 0 && duplicates.Count == 0 && unreadable.Count == 0,
            missing,
            duplicates,
            unreadable,
            lowest,
            highest);
    }
}
