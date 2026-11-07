namespace Ostagar.Modules.DocumentWorkflow;

/// <summary>
/// A run of counter values that no document in the period holds, inclusive at both ends.
/// </summary>
/// <remarks>
/// A range rather than a list of values, because the report has to stay cheap to produce on a period the
/// tenant suspects has been tampered with. Enumerating the missing values of a row whose counter was
/// edited to a billion would build a list of a billion ints — so the honest version of an audit report
/// states the hole it found, and a hole in a numbering series is a span, not a count.
/// </remarks>
/// <param name="From">First missing value.</param>
/// <param name="To">Last missing value, inclusive, never below <paramref name="From"/>.</param>
public readonly record struct NumberingValueRange(int From, int To)
{
    /// <summary>How many numbers this span covers, without materialising them.</summary>
    public int Count => To - From + 1;

    public override string ToString() =>
        From == To
            ? From.ToString(System.Globalization.CultureInfo.InvariantCulture)
            : $"{From}-{To}";
}
