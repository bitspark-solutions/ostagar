using System.Globalization;

namespace Ostagar.Modules.DocumentWorkflow;

/// <summary>
/// Renders a counter value into the string that goes on the paper, and reads it back.
/// </summary>
/// <remarks>
/// The single place the printed shape of a number is decided, which is the point: the legacy tree
/// assembled voucher numbers in three services, one of which padded to two digits and two of which did
/// not, so the same series contained <c>JE-2025-01-9</c> and <c>JE-2025-01-10</c> and ordering them as
/// text put the earlier voucher last. An auditor reading the list in that order sees a gap that does
/// not exist and a duplicate that does not either.
///
/// Rendering is strict, not forgiving: a value that needs more digits than the width offers is refused
/// by the caller (<see cref="NumberingErrorCode.ValueOutgrowsPadding"/>) rather than rendered wider,
/// because both alternatives lose information.
/// </remarks>
public static class NumberingFormat
{
    public const char Separator = '-';

    /// <summary>How many digits a value occupies. A loop, because logarithms and floats are not money-adjacent tools.</summary>
    public static int DigitsOf(int value)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, "A counter value cannot be negative.");
        }

        var digits = 1;
        var remaining = value;

        while (remaining >= 10)
        {
            remaining /= 10;
            digits++;
        }

        return digits;
    }

    /// <summary>Whether the value fits the width without changing the printed length of the series.</summary>
    public static bool Fits(int width, int value) => DigitsOf(value) <= width;

    /// <summary>
    /// Builds the number: <c>PREFIX</c>, then the period when the scheme has one, then the zero-padded value.
    /// </summary>
    public static string Render(string prefix, NumberingPeriodKey period, int width, int value)
    {
        if (!NumberingSchemeRules.IsPrefixWellFormed(prefix))
        {
            throw new ArgumentException($"'{prefix}' is not a storable prefix.", nameof(prefix));
        }

        if (!Fits(width, value))
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                value,
                $"Value {value.ToString(CultureInfo.InvariantCulture)} needs more than {width.ToString(CultureInfo.InvariantCulture)} digits.");
        }

        var padded = value.ToString("D" + width.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);

        return period.IsEmpty
            ? prefix + Separator + padded
            : prefix + Separator + period.Code + Separator + padded;
    }

    /// <summary>
    /// Reads a printed number back to its counter value, accepting only a string this renderer would
    /// have produced for it.
    /// </summary>
    /// <remarks>
    /// The round trip is the check. A number stored with the wrong padding, a different prefix, or a
    /// period that no longer matches its scheme fails here, which is what makes the gap audit
    /// trustworthy: it cannot be satisfied by a row someone typed in by hand.
    /// </remarks>
    public static bool TryParseValue(string prefix, NumberingPeriodKey period, int width, string? number, out int value)
    {
        value = 0;

        if (string.IsNullOrEmpty(number) || !number.StartsWith(prefix + Separator, StringComparison.Ordinal))
        {
            return false;
        }

        var remainder = number.AsSpan(prefix.Length + 1);

        if (!period.IsEmpty)
        {
            var expectedPeriod = period.Code + Separator;

            if (remainder.Length < expectedPeriod.Length
                || !remainder[..expectedPeriod.Length].Equals(expectedPeriod.AsSpan(), StringComparison.Ordinal))
            {
                return false;
            }

            remainder = remainder[expectedPeriod.Length..];
        }

        if (remainder.Length < width)
        {
            return false;
        }

        var digits = remainder.ToString();

        foreach (var character in digits)
        {
            if (!char.IsAsciiDigit(character))
            {
                return false;
            }
        }

        if (!int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed))
        {
            return false;
        }

        var rendered = Render(prefix, period, width, parsed);
        value = parsed;

        return string.Equals(rendered, number, StringComparison.Ordinal);
    }

    private const int MaxWidthForLong = 19;
}
