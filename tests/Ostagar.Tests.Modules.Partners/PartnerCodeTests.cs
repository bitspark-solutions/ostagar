using Ostagar.Core;
using Ostagar.Modules.Partners;

namespace Ostagar.Tests.Modules.Partners;

/// <summary>
/// A partner code is a sequence value and nothing else. The legacy project generated codes from typed names
/// with <c>new Random().Next(100, 999)</c>, which is how a chart of accounts and a party list end up with
/// collisions nobody can explain — so there is no path in this module that turns text into a code.
/// </summary>
public sealed class PartnerCodeTests
{
    [Theory]
    [InlineData("SONALI COTTON")]
    [InlineData("সোনালী কটন")]
    [InlineData("AB")]
    [InlineData("1")]
    [InlineData("PRT 1001")]
    [InlineData("PRT@1001")]
    public void Text_that_is_not_an_allocated_code_is_refused(string candidate)
    {
        Assert.False(PartnerCode.TryFrom(candidate, out _));
    }

    [Theory]
    [InlineData("PRT-1001")]
    [InlineData("prt-1001")]
    [InlineData("10001")]
    [InlineData("CUS.00456")]
    public void An_allocated_code_is_accepted_and_stored_in_its_canonical_form(string candidate)
    {
        var code = PartnerCode.From(candidate);

        Assert.Equal(candidate.ToUpperInvariant(), code.Value);
    }

    [Fact]
    public void A_code_carrying_the_party_s_own_name_is_refused_at_registration()
    {
        var draft = new NewPartner(
            PartnerFixture.Tenant,
            PartnerId.New(),
            PartnerCode.From("SONALI-0007"),
            PartnerName.From("Sonali Cotton Mills Limited", "Legal name"),
            PartnerIdentificationBasis.AuthoritativeDocument,
            [new PartnerIdentifier(PartnerIdentifierKind.TaxIdentificationNumber, "1234567890")],
            [CurrencyCode.Bdt],
            PartnerFixture.RegisteredOn);

        var refusal = Assert.Throws<PartnerRuleException>(() => Partner.Register(draft));

        Assert.Equal(PartnerErrorCode.CodeDerivedFromName, refusal.Code);
    }

    [Fact]
    public void A_short_role_prefix_is_not_mistaken_for_a_name()
    {
        var partner = PartnerFixture.TradeLicencePartner(code: "VEN-7001", legalName: "Venus Garments Limited");

        Assert.Equal("VEN-7001", partner.Code.Value);

        var sameSequenceTwice = PartnerCode.From("VEN-7001");

        Assert.Equal(partner.Code, sameSequenceTwice);
    }

    [Fact]
    public void A_rename_can_never_make_the_code_false()
    {
        var partner = PartnerFixture.TradeLicencePartner(code: "PRT-7007", legalName: "Padma Fashions Limited");

        partner.ChangeLegalName(PartnerName.From("Padma Fashions and Exports Limited", "Legal name"));
        partner.ChangeTradingName(PartnerName.From("Padma Fashions", "Trading name"));

        Assert.Equal("PRT-7007", partner.Code.Value);
        Assert.Equal(PartnerCode.From("PRT-7007"), partner.Code);
    }

    [Fact]
    public void The_identifier_a_partner_is_known_by_and_the_name_it_is_called_by_are_different_things()
    {
        var partner = PartnerFixture.TradeLicencePartner(code: "PRT-7008", legalName: "Rahima Garments");

        partner.AddIdentifier(new PartnerIdentifier(PartnerIdentifierKind.VatRegistrationNumber, "0258963147001"));

        Assert.Equal("PRT-7008", partner.Code.Value);
        Assert.Equal("Rahima Garments", partner.LegalName.Value);
        Assert.Equal(3, partner.IdentificationForMatching().Count);
        Assert.DoesNotContain(partner.IdentificationForMatching(), identifier => identifier.Value.Contains("Rahima", StringComparison.Ordinal));
    }
}
