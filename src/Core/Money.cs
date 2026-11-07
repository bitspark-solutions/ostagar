using System.Globalization;

namespace Ostagar.Core;

/// <summary>
/// An amount in one currency, at that currency's scale.
/// </summary>
/// <remarks>
/// Three decisions here carry the accounting correctness requirement:
///
/// <para><b>Never a bare decimal.</b> Two amounts cannot be added unless they name the same currency,
/// so the class of bug where a BDT total silently absorbed a USD figure cannot be written.</para>
///
/// <para><b>No implicit rounding.</b> Constructing with more decimal places than the currency allows is
/// an error, not a quiet truncation. Rounding is a separate, named operation, because in a ledger the
/// question "who decided to drop this paisa, and where did the difference go" is answerable or it is a
/// reconciliation defect. The legacy validator tolerated a 0.01 imbalance, which is one whole minor
/// unit — enough to hide a rounding policy that did not exist.</para>
///
/// <para><b>No <c>float</c> or <c>double</c> anywhere near it.</b></para>
/// </remarks>
public readonly record struct Money : IComparable<Money>
{
    public Currency Currency { get; }

    public decimal Amount { get; }

    public Money(Currency currency, decimal amount)
    {
        Currency = currency;

        if (decimal.Truncate(amount) != amount
            && CountDecimalPlaces(amount) > currency.MinorDigits)
        {
            throw new ArgumentException(
                $"{currency.Code} carries {currency.MinorDigits} minor digit(s) but the amount {amount.ToString(CultureInfo.InvariantCulture)} "
                + "has more. Round it explicitly with Money.Round so the rounding decision is visible at the call site.",
                nameof(amount));
        }

        Amount = Normalise(amount, currency.MinorDigits);
    }

    public static Money Zero(Currency currency) => new(currency, 0m);

    public static Money Bdt(decimal amount) => new(Currency.Of(CurrencyCode.Bdt), amount);

    public bool IsZero => Amount == 0m;

    public bool IsNegative => Amount < 0m;

    public bool IsPositive => Amount > 0m;

    public Money Negate() => new(Currency, -Amount);

    /// <summary>
    /// Rounds to the currency's scale using banker's rounding.
    /// </summary>
    /// <remarks>
    /// Deliberately explicit and deliberately not called by any constructor. Half-up is the convention
    /// people expect on an invoice and banker's is the convention that does not bias a whole ledger;
    /// picking one silently is worse than either. The residue this leaves is what the header rounding
    /// account exists to receive.
    /// </remarks>
    public static Money Round(Currency currency, decimal amount) =>
        new(currency, decimal.Round(amount, currency.MinorDigits, MidpointRounding.ToEven));

    public static Money Add(Money left, Money right)
    {
        RequireSameCurrency(left, right, nameof(Add));
        return new Money(left.Currency, left.Amount + right.Amount);
    }

    public static Money Subtract(Money left, Money right)
    {
        RequireSameCurrency(left, right, nameof(Subtract));
        return new Money(left.Currency, left.Amount - right.Amount);
    }

    public static Money operator +(Money left, Money right) => Add(left, right);

    public static Money operator -(Money left, Money right) => Subtract(left, right);

    public static Money operator -(Money value) => value.Negate();

    public Money Abs() => IsNegative ? Negate() : this;

    public int CompareTo(Money other)
    {
        RequireSameCurrency(this, other, nameof(CompareTo));
        return Amount.CompareTo(other.Amount);
    }

    public static bool operator <(Money left, Money right) => left.CompareTo(right) < 0;

    public static bool operator >(Money left, Money right) => left.CompareTo(right) > 0;

    public static bool operator <=(Money left, Money right) => left.CompareTo(right) <= 0;

    public static bool operator >=(Money left, Money right) => left.CompareTo(right) >= 0;

    public override string ToString() =>
        $"{Amount.ToString("N" + Currency.MinorDigits, CultureInfo.InvariantCulture)} {Currency.Code}";

    /// <summary>
    /// The amount expressed in the smallest unit of its currency — 1234 for ₹12.34.
    /// </summary>
    /// <remarks>
    /// Integer minor units are how a ledger avoids a whole class of accumulation error, and how the
    /// database constraints in the posting engine compare sums without decimal drift.
    /// </remarks>
    public long ToMinorUnits()
    {
        var scale = 1L;

        for (var index = 0; index < Currency.MinorDigits; index++)
        {
            scale *= 10L;
        }

        return checked((long)(Amount * scale));
    }

    private static void RequireSameCurrency(Money left, Money right, string operation)
    {
        if (left.Currency.Code != right.Currency.Code)
        {
            throw new ArgumentException(
                $"{operation} cannot mix {left.Currency.Code} with {right.Currency.Code}. "
                + "Convert through an explicit rate with an effective date instead.");
        }
    }

    private static int CountDecimalPlaces(decimal value)
    {
        var text = value.ToString(CultureInfo.InvariantCulture);
        var separator = text.IndexOf('.');

        return separator < 0 ? 0 : text.Length - separator - 1;
    }

    private static decimal Normalise(decimal amount, int minorDigits) =>
        minorDigits == 0 ? decimal.Truncate(amount) : decimal.Round(amount, minorDigits, MidpointRounding.ToEven);
}
