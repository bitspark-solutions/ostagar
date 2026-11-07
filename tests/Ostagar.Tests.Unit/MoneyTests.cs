using Ostagar.Core;

namespace Ostagar.Tests.Unit;

public class MoneyTests
{
    [Fact]
    public void Rejects_more_precision_than_the_currency_allows_instead_of_quietly_truncating()
    {
        var bdt = Currency.Of(CurrencyCode.Bdt);

        var error = Assert.Throws<ArgumentException>(() => new Money(bdt, 10.005m));

        Assert.Contains("Round it explicitly", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Rounds_only_when_asked_to()
    {
        var rounded = Money.Round(Currency.Of(CurrencyCode.Bdt), 10.005m);

        Assert.Equal(10.00m, rounded.Amount);
    }

    [Fact]
    public void A_currency_with_no_minor_unit_still_refuses_a_fractional_amount()
    {
        var jpy = Currency.Of(CurrencyCode.From("JPY"));

        Assert.Equal(0, jpy.MinorDigits);

        // The scale of the currency does not license silently dropping a fraction. A unit price with
        // paise on it has to be rounded by someone who decides to round it.
        Assert.Throws<ArgumentException>(() => new Money(jpy, 1234.9m));
        Assert.Equal(1235m, Money.Round(jpy, 1234.9m).Amount);
    }

    [Fact]
    public void A_currency_with_three_minor_digits_accepts_three_and_refuses_four()
    {
        var bhd = Currency.Of(CurrencyCode.From("BHD"));

        Assert.Equal(3, bhd.MinorDigits);
        Assert.Equal(1.234m, new Money(bhd, 1.234m).Amount);

        Assert.Throws<ArgumentException>(() => new Money(bhd, 1.2345m));

        // Banker's rounding takes this DOWN to 1.234, not up to 1.235. Asserting the expected value
        // here is the point: the rounding rule is a policy, it is visible at the call site, and a
        // reader who assumed half-up learns immediately that this ledger does not do that.
        Assert.Equal(1.234m, Money.Round(bhd, 1.2345m).Amount);
    }

    [Fact]
    public void Refuses_to_add_two_currencies()
    {
        var taka = Money.Bdt(10m);
        var dollars = new Money(Currency.Of(CurrencyCode.Usd), 10m);

        Assert.Throws<ArgumentException>(() => taka + dollars);
    }

    [Fact]
    public void Exposes_integer_minor_units_for_exact_summing()
    {
        Assert.Equal(1234L, Money.Bdt(12.34m).ToMinorUnits());
        Assert.Equal(1234L, new Money(Currency.Of(CurrencyCode.From("JPY")), 1234m).ToMinorUnits());
    }

    [Fact]
    public void Currency_code_is_normalised_and_validated()
    {
        Assert.Equal("BDT", CurrencyCode.From("bdt").Value);
        Assert.False(CurrencyCode.TryFrom("Rupi", out _));
        Assert.False(CurrencyCode.TryFrom(null, out _));
    }

    [Fact]
    public void Two_amounts_are_equal_only_in_the_same_currency()
    {
        Assert.Equal(Money.Bdt(5m), Money.Bdt(5m));
        Assert.NotEqual(Money.Bdt(5m), new Money(Currency.Of(CurrencyCode.Usd), 5m));
    }
}
