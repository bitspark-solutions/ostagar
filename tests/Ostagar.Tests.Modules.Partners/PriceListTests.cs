using Ostagar.Core;
using Ostagar.Modules.Partners;

namespace Ostagar.Tests.Modules.Partners;

/// <summary>
/// A price is an agreement, so it is quoted from a list that exists, in a currency the party is traded in,
/// over a period someone has to renew. Every one of those is a refusal here rather than a fallback.
/// </summary>
public sealed class PriceListTests
{
    private static readonly DateOnly From = new(2026, 11, 1);

    private static readonly DateOnly To = new(2027, 4, 30);

    [Fact]
    public void A_price_list_in_a_currency_the_partner_is_not_traded_in_is_refused()
    {
        var partner = PartnerFixture.TradeLicencePartner();

        partner.GrantCustomerRole(PartnerFixture.CustomerTerms());

        var refusal = Assert.Throws<PartnerRuleException>(() => partner.IssuePriceList(
            PriceListDirection.Sales,
            PartnerFixture.Usd,
            From,
            To,
            [PartnerFixture.Line(PartnerFixture.NewStyle(), 24m, PartnerFixture.Usd)]));

        Assert.Equal(PartnerErrorCode.PriceListCurrencyNotTraded, refusal.Code);
        Assert.Empty(partner.PriceLists);
    }

    [Fact]
    public void A_line_in_another_currency_than_its_list_is_refused()
    {
        var partner = PartnerFixture.TradeLicencePartner(currencies: [CurrencyCode.Bdt, CurrencyCode.Usd]);

        partner.GrantCustomerRole(PartnerFixture.CustomerTerms());

        var refusal = Assert.Throws<PartnerRuleException>(() => partner.IssuePriceList(
            PriceListDirection.Sales,
            PartnerFixture.Bdt,
            From,
            To,
            [PartnerFixture.Line(PartnerFixture.NewStyle(), 24m, PartnerFixture.Usd)]));

        Assert.Equal(PartnerErrorCode.PriceListLineCurrencyMismatch, refusal.Code);
    }

    [Fact]
    public void A_list_has_to_be_valid_on_at_least_one_day()
    {
        var partner = PartnerFixture.TradeLicencePartner();

        partner.GrantCustomerRole(PartnerFixture.CustomerTerms());

        var refusal = Assert.Throws<PartnerRuleException>(() => partner.IssuePriceList(
            PriceListDirection.Sales,
            PartnerFixture.Bdt,
            From,
            From,
            [PartnerFixture.Line(PartnerFixture.NewStyle(), 420m)]));

        Assert.Equal(PartnerErrorCode.PriceListValidityInvalid, refusal.Code);

        Assert.Throws<PartnerRuleException>(() => partner.IssuePriceList(
            PriceListDirection.Sales,
            PartnerFixture.Bdt,
            To,
            From,
            [PartnerFixture.Line(PartnerFixture.NewStyle(), 420m)]));
    }

    [Fact]
    public void The_expiry_day_still_prices_and_the_day_after_does_not()
    {
        var partner = PartnerFixture.TradeLicencePartner();

        partner.GrantCustomerRole(PartnerFixture.CustomerTerms());

        var style = PartnerFixture.NewStyle();
        partner.IssuePriceList(
            PriceListDirection.Sales,
            PartnerFixture.Bdt,
            From,
            To,
            [PartnerFixture.Line(style, 420m)]);

        Assert.Equal(420m, partner.RequirePrice(style, PriceListDirection.Sales, CurrencyCode.Bdt, To).UnitPrice.Amount);

        var refusal = Assert.Throws<PartnerRuleException>(
            () => partner.RequirePrice(style, PriceListDirection.Sales, CurrencyCode.Bdt, To.AddDays(1)));

        Assert.Equal(PartnerErrorCode.PriceListExpired, refusal.Code);
    }

    [Fact]
    public void Two_lists_covering_the_same_days_in_the_same_currency_are_refused()
    {
        var partner = PartnerFixture.TradeLicencePartner();

        partner.GrantCustomerRole(PartnerFixture.CustomerTerms());

        partner.IssuePriceList(
            PriceListDirection.Sales,
            PartnerFixture.Bdt,
            From,
            To,
            [PartnerFixture.Line(PartnerFixture.NewStyle(), 420m)]);

        var refusal = Assert.Throws<PartnerRuleException>(() => partner.IssuePriceList(
            PriceListDirection.Sales,
            PartnerFixture.Bdt,
            To.AddDays(-30),
            To.AddDays(30),
            [PartnerFixture.Line(PartnerFixture.NewStyle(), 440m)]));

        Assert.Equal(PartnerErrorCode.PriceListOverlap, refusal.Code);
        Assert.Single(partner.PriceLists);
    }

    [Fact]
    public void A_style_that_was_never_agreed_is_refused_rather_than_quoted_at_nothing()
    {
        var partner = PartnerFixture.TradeLicencePartner();

        partner.GrantCustomerRole(PartnerFixture.CustomerTerms());

        partner.IssuePriceList(
            PriceListDirection.Sales,
            PartnerFixture.Bdt,
            From,
            To,
            [PartnerFixture.Line(PartnerFixture.NewStyle(), 420m)]);

        var refusal = Assert.Throws<PartnerRuleException>(
            () => partner.RequirePrice(PartnerFixture.NewStyle(), PriceListDirection.Sales, CurrencyCode.Bdt, From));

        Assert.Equal(PartnerErrorCode.StyleNotPriced, refusal.Code);
    }

    [Fact]
    public void A_price_of_zero_is_a_missing_price_and_a_blank_style_is_a_missing_reference()
    {
        Assert.Throws<ArgumentException>(() => new PriceListLine(PartnerFixture.NewStyle(), Money.Bdt(0m)));
        Assert.Throws<ArgumentException>(() => new PriceListLine(default(StyleId), Money.Bdt(10m)));
        Assert.Throws<ArgumentException>(() => new PriceListLine(PartnerFixture.NewStyle(), Money.Bdt(-5m)));
    }

    [Fact]
    public void A_list_with_no_lines_is_refused()
    {
        var partner = PartnerFixture.TradeLicencePartner();

        partner.GrantCustomerRole(PartnerFixture.CustomerTerms());

        var refusal = Assert.Throws<PartnerRuleException>(
            () => partner.IssuePriceList(PriceListDirection.Sales, PartnerFixture.Bdt, From, To, []));

        Assert.Equal(PartnerErrorCode.PriceListEmpty, refusal.Code);
    }

    [Fact]
    public void One_style_cannot_be_priced_twice_on_one_list()
    {
        var partner = PartnerFixture.TradeLicencePartner();

        partner.GrantCustomerRole(PartnerFixture.CustomerTerms());

        var style = PartnerFixture.NewStyle();

        var refusal = Assert.Throws<PartnerRuleException>(() => partner.IssuePriceList(
            PriceListDirection.Sales,
            PartnerFixture.Bdt,
            From,
            To,
            [PartnerFixture.Line(style, 420m), PartnerFixture.Line(style, 450m)]));

        Assert.Equal(PartnerErrorCode.PriceListStyleDuplicated, refusal.Code);
    }

    [Fact]
    public void The_discount_and_the_net_price_re_add_to_the_quoted_price()
    {
        var line = PartnerFixture.Line(PartnerFixture.NewStyle(), 1_234.56m, discount: 33.3333m);

        Assert.Equal(411.52m, line.DiscountAmount().Amount);
        Assert.Equal(823.04m, line.NetUnitPrice().Amount);
        Assert.Equal(1_234.56m, Money.Add(line.DiscountAmount(), line.NetUnitPrice()).Amount);
        Assert.Equal(CurrencyCode.Bdt, line.Currency);
    }

    [Fact]
    public void A_fully_discounted_line_is_a_stated_decision_and_nets_to_zero()
    {
        var line = PartnerFixture.Line(PartnerFixture.NewStyle(), 500m, discount: 100m);

        Assert.True(line.NetUnitPrice().IsZero);
        Assert.Throws<ArgumentException>(() => new PriceListLine(PartnerFixture.NewStyle(), Money.Bdt(500m), 100.0001m));
    }

    [Fact]
    public void An_inactive_partner_cannot_be_issued_a_new_list()
    {
        var partner = PartnerFixture.TradeLicencePartner();

        partner.GrantCustomerRole(PartnerFixture.CustomerTerms());
        partner.Deactivate(hasOpenDocuments: false, PartnerFixture.RegisteredOn);

        var refusal = Assert.Throws<PartnerRuleException>(
            () => partner.IssuePriceList(PriceListDirection.Sales, PartnerFixture.Bdt, From, To, [PartnerFixture.Line(PartnerFixture.NewStyle(), 420m)]));

        Assert.Equal(PartnerErrorCode.PartnerInactive, refusal.Code);
    }

    [Fact]
    public void One_partner_may_be_priced_in_each_currency_it_trades_in()
    {
        var partner = PartnerFixture.TradeLicencePartner(currencies: [CurrencyCode.Bdt, CurrencyCode.Usd]);

        partner.GrantCustomerRole(PartnerFixture.CustomerTerms());

        partner.IssuePriceList(PriceListDirection.Sales, PartnerFixture.Bdt, From, To, [PartnerFixture.Line(PartnerFixture.NewStyle(), 420m)]);

        var usdStyle = PartnerFixture.NewStyle();
        partner.IssuePriceList(
            PriceListDirection.Sales,
            PartnerFixture.Usd,
            From,
            To,
            [PartnerFixture.Line(usdStyle, 4m, PartnerFixture.Usd)]);

        Assert.Equal(2, partner.PriceLists.Count);
        Assert.Equal(4m, partner.RequirePrice(usdStyle, PriceListDirection.Sales, CurrencyCode.Usd, From).UnitPrice.Amount);
    }

    [Fact]
    public void A_currency_still_being_quoted_cannot_be_withdrawn_from_the_partner()
    {
        var partner = PartnerFixture.TradeLicencePartner(currencies: [CurrencyCode.Bdt, CurrencyCode.Usd]);

        partner.GrantCustomerRole(PartnerFixture.CustomerTerms());

        partner.IssuePriceList(
            PriceListDirection.Sales,
            PartnerFixture.Usd,
            From,
            To,
            [PartnerFixture.Line(PartnerFixture.NewStyle(), 4m, PartnerFixture.Usd)]);

        var refusal = Assert.Throws<PartnerRuleException>(
            () => partner.SetDocumentCurrencies([CurrencyCode.Bdt], new DateOnly(2026, 12, 1)));

        Assert.Equal(PartnerErrorCode.DocumentCurrencyInUse, refusal.Code);

        partner.SetDocumentCurrencies([CurrencyCode.Bdt], new DateOnly(2027, 6, 1));

        Assert.Equal(CurrencyCode.Bdt, Assert.Single(partner.DocumentCurrencies));
    }
}
