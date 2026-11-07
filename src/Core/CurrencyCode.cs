namespace Ostagar.Core;

/// <summary>
/// An ISO 4217 currency code, validated on construction.
/// </summary>
/// <remarks>
/// A three-letter string is the shape every money bug in this project's history started from: the
/// legacy trial balance hardcoded <c>USD</c> and <c>en-US</c> in the renderer while the balance screen
/// beside it hardcoded <c>BDT</c>, and adjacent financial reports disagreed about which company's
/// currency they were in. Making the code a type means a function cannot be handed "Rupi" or an empty
/// string.
/// </remarks>
public readonly record struct CurrencyCode : IEquatable<CurrencyCode>
{
    private readonly string _value;

    private CurrencyCode(string value) => _value = value;

    /// <summary>Bangladeshi taka — the functional currency for every tenant in v1.</summary>
    public static CurrencyCode Bdt { get; } = new("BDT");

    public static CurrencyCode Usd { get; } = new("USD");

    public static CurrencyCode Eur { get; } = new("EUR");

    public static CurrencyCode Gbp { get; } = new("GBP");

    public static CurrencyCode Cny { get; } = new("CNY");

    public static CurrencyCode Inr { get; } = new("INR");

    public string Value => _value ?? string.Empty;

    public bool IsEmpty => _value is null;

    public static CurrencyCode From(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException("A currency code is required.", nameof(code));
        }

        var normalised = code.Trim().ToUpperInvariant();

        if (normalised.Length != 3 || !normalised.All(char.IsLetter))
        {
            throw new ArgumentException(
                $"'{code}' is not a three-letter ISO 4217 currency code.", nameof(code));
        }

        return new CurrencyCode(normalised);
    }

    public static bool TryFrom(string? code, out CurrencyCode result)
    {
        try
        {
            result = From(code ?? string.Empty);
            return true;
        }
        catch (ArgumentException)
        {
            result = default;
            return false;
        }
    }

    public override string ToString() => Value;
}
