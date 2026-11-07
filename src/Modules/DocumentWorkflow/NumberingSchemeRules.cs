using Ostagar.Ledger;

namespace Ostagar.Modules.DocumentWorkflow;

/// <summary>
/// The shape a numbering scheme must have before any number can be rendered from it.
/// </summary>
/// <remarks>
/// Checked at allocation as well as at edit time. A scheme row can be written by an import, a SQL
/// correction or an older build, and the allocator must not be the component that discovers the prefix
/// is a space: a number that reaches paper cannot be unsaid.
/// </remarks>
public static class NumberingSchemeRules
{
    /// <summary>Long enough for <c>PURCHASE</c>, short enough to keep the number quotable on paper.</summary>
    public const int MaxPrefixLength = 12;

    /// <summary>Below one there is no number to print; above twelve the counter cannot fit a column.</summary>
    public const int MinWidth = 1;

    public const int MaxWidth = 12;

    /// <summary>
    /// Every defect a scheme row carries, reported together so a settings screen can show the whole list.
    /// </summary>
    public static IReadOnlyList<NumberingError> ValidateShape(DocumentNumberingScheme scheme)
    {
        ArgumentNullException.ThrowIfNull(scheme);

        var errors = new List<NumberingError>();

        if (!IsPrefixWellFormed(scheme.Prefix))
        {
            errors.Add(new NumberingError(
                NumberingErrorCode.InvalidPrefix,
                $"The prefix '{scheme.Prefix}' must be 1 to {MaxPrefixLength} characters of A-Z, 0-9 or '-'."));
        }

        if (scheme.Width is < MinWidth or > MaxWidth)
        {
            errors.Add(new NumberingError(
                NumberingErrorCode.WidthOutOfRange,
                $"The padding width must be between {MinWidth} and {MaxWidth}."));
        }

        if (scheme.StartingValue < 1)
        {
            errors.Add(new NumberingError(
                NumberingErrorCode.StartingValueBelowOne,
                "A sequence starts at one or above; a zero or negative value has no printed form."));
        }

        if (scheme.FiscalStartMonth is < 1 or > 12)
        {
            errors.Add(new NumberingError(
                NumberingErrorCode.InvalidFiscalStartMonth,
                $"The fiscal year must start in a month between 1 and 12, like every period the ledger uses (July is {FiscalCalendar.DefaultStartMonth})."));
        }

        return errors;
    }

    /// <summary>
    /// A prefix is upper-case alphanumerics with hyphen separators: stored exactly as it prints, so no
    /// renderer has to normalise anything and two tenants' spellings cannot differ only in case.
    /// </summary>
    public static bool IsPrefixWellFormed(string? prefix)
    {
        if (string.IsNullOrEmpty(prefix) || prefix.Length > MaxPrefixLength)
        {
            return false;
        }

        foreach (var character in prefix)
        {
            if (!char.IsAsciiLetterUpper(character) && !char.IsAsciiDigit(character) && character != '-')
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Normalises tenant input into the stored form, so an edit screen cannot save <c>so </c>.</summary>
    public static string NormalizePrefix(string? prefix) =>
        (prefix ?? string.Empty).Trim().ToUpperInvariant();
}
