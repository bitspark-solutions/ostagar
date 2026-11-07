namespace Ostagar.Core;

/// <summary>
/// A currency and the number of decimal places its amounts carry.
/// </summary>
/// <remarks>
/// The legacy tree used <c>decimal(18,2)</c> for everything. That is wrong for a quarter of the world's
/// currencies — JPY and KRW have no minor unit, BHD and KWD have three — and a scale mismatch is not a
/// display problem: it silently rounds amounts that should not be rounded, and it makes a converted
/// amount impossible to reconcile back to its original.
/// </remarks>
public readonly record struct Currency
{
    /// <summary>ISO 4217 minor-unit exponent: 2 for BDT, 0 for JPY, 3 for BHD.</summary>
    public int MinorDigits { get; init; }

    public CurrencyCode Code { get; init; }

    public Currency(CurrencyCode code, int minorDigits)
    {
        if (code.IsEmpty)
        {
            throw new ArgumentException("A currency needs an ISO 4217 code.", nameof(code));
        }

        if (minorDigits is < 0 or > 4)
        {
            throw new ArgumentOutOfRangeException(
                nameof(minorDigits),
                "Minor-unit digits must be between 0 and 4.");
        }

        Code = code;
        MinorDigits = minorDigits;
    }

    public static Currency Of(CurrencyCode code) => new(code, MinorDigitsFor(code));

    /// <summary>
    /// Known exponents. Anything unlisted defaults to two digits rather than being guessed at, and the
    /// caller can override it explicitly — defaulting to a wrong scale is how money quietly goes missing.
    /// </summary>
    public static int MinorDigitsFor(CurrencyCode code) => code.Value switch
    {
        "JPY" or "KRW" or "VND" or "PKR" => 0,
        "BHD" or "KWD" or "OMR" or "TND" => 3,
        _ => 2,
    };

    /// <summary>The smallest amount this currency can represent, e.g. 0.01 for BDT.</summary>
    public decimal SmallestUnit => 1m / PowerOfTen(MinorDigits);

    public Money Zero() => Money.Zero(this);

    private static decimal PowerOfTen(int exponent)
    {
        var result = 1m;

        for (var index = 0; index < exponent; index++)
        {
            result *= 10m;
        }

        return result;
    }
}
