using Ostagar.Core;
using Ostagar.Modules.Partners;

namespace Ostagar.Tests.Modules.Partners;

/// <summary>
/// A rename has to be a rename. The party on a two-year-old invoice is the same business whatever its name
/// now says, and the moment a rename mints a new identity, the aged-receivables report splits one customer into
/// two and neither figure is the truth.
/// </summary>
public sealed class PartnerRenameTests
{
    [Fact]
    public void Renaming_the_legal_name_leaves_identity_and_code_untouched()
    {
        var partner = PartnerFixture.TradeLicencePartner(code: "PRT-7001");

        partner.GrantCustomerRole(PartnerFixture.CustomerTerms(creditLimit: Money.Bdt(900_000m)));

        var style = PartnerFixture.NewStyle();
        var list = partner.IssuePriceList(
            PriceListDirection.Sales,
            PartnerFixture.Bdt,
            PartnerFixture.Today,
            PartnerFixture.Today.AddMonths(6),
            [PartnerFixture.Line(style, 640m)]);

        var identity = partner.Id;
        var code = partner.Code;

        partner.ChangeLegalName(PartnerName.From("Sonali Cotton Mills Limited and Exports", "Legal name"));

        Assert.Equal(identity, partner.Id);
        Assert.Equal(code, partner.Code);
        Assert.Equal("Sonali Cotton Mills Limited and Exports", partner.LegalName.Value);
        Assert.Equal(identity, list.Owner);
        Assert.Equal(640m, partner.RequirePrice(style, PriceListDirection.Sales, CurrencyCode.Bdt, PartnerFixture.Today).UnitPrice.Amount);
    }

    [Fact]
    public void The_three_names_are_three_fields_and_each_moves_alone()
    {
        var partner = PartnerFixture.TradeLicencePartner(legalName: "Rupganj Textiles Limited");

        partner.ChangeTradingName(PartnerName.From("Rupganj Textiles", "Trading name"));
        partner.ChangeBanglaName(BanglaName.From("রূপগঞ্জ টেক্সটাইলস লিমিটেড"));

        Assert.Equal("Rupganj Textiles Limited", partner.LegalName.Value);
        Assert.Equal("Rupganj Textiles", partner.TradingName?.Value);
        Assert.Equal("রূপগঞ্জ টেক্সটাইলস লিমিটেড", partner.BanglaName?.Value);

        partner.ChangeLegalName(PartnerName.From("Rupganj Textiles and Dyeing Limited", "Legal name"));

        Assert.Equal("Rupganj Textiles", partner.TradingName?.Value);
        Assert.Equal("রূপগঞ্জ টেক্সটাইলস লিমিটেড", partner.BanglaName?.Value);
    }

    [Fact]
    public void A_trading_name_that_repeats_the_legal_name_is_refused_rather_than_stored_twice()
    {
        var partner = PartnerFixture.TradeLicencePartner(legalName: "Meghna Apparels Limited");

        var refusal = Assert.Throws<PartnerRuleException>(
            () => partner.ChangeTradingName(PartnerName.From("Meghna Apparels Limited", "Trading name")));

        Assert.Equal(PartnerErrorCode.TradingNameRedundant, refusal.Code);

        partner.ChangeTradingName(null);

        Assert.Null(partner.TradingName);
    }

    [Fact]
    public void Renaming_into_the_code_is_refused_because_the_code_came_first()
    {
        var partner = PartnerFixture.TradeLicencePartner(code: "MEGHNA-9", legalName: "Capital Garments Limited");

        var refusal = Assert.Throws<PartnerRuleException>(
            () => partner.ChangeLegalName(PartnerName.From("Meghna Garments Limited", "Legal name")));

        Assert.Equal(PartnerErrorCode.CodeDerivedFromName, refusal.Code);
        Assert.Equal("Capital Garments Limited", partner.LegalName.Value);
    }

    [Fact]
    public void A_deactivated_partner_still_resolves_to_its_name_and_history()
    {
        var partner = PartnerFixture.TradeLicencePartner(legalName: "Jamuna Denims Limited");

        partner.Deactivate(hasOpenDocuments: false, PartnerFixture.RegisteredOn.AddDays(30));

        Assert.Equal(PartnerStatus.Inactive, partner.Status);
        Assert.Equal("Jamuna Denims Limited", partner.LegalName.Value);
        Assert.Equal(PartnerFixture.RegisteredOn.AddDays(30), partner.InactiveOn);
        Assert.False(partner.IsActive);
    }
}
