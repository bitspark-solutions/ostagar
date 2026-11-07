using Ostagar.Modules.Partners;

namespace Ostagar.Tests.Modules.Partners;

/// <summary>
/// The buyer of this product is a Bangladeshi factory owner whose counterparties are named in Bangla. A
/// validator that accepted only ASCII would force a transliteration into the master record, and the owner would
/// lose the ability to find the party they have traded with for years. These tests pin the script rules.
/// </summary>
public sealed class PartnerNameValidationTests
{
    [Fact]
    public void A_legal_name_written_in_Bangla_script_is_a_first_class_value()
    {
        var name = PartnerName.From("সোনালী কটন মিলস লিমিটেড", "Legal name");

        Assert.Equal("সোনালী কটন মিলস লিমিটেড", name.Value);
        Assert.True(name.IsBangla);
    }

    [Fact]
    public void A_partner_can_be_registered_on_its_Bangla_name_alone()
    {
        var partner = Partner.Register(new NewPartner(
            PartnerFixture.Tenant,
            PartnerId.New(),
            PartnerCode.From("PRT-9100"),
            PartnerName.From("রূপগঞ্জ নিট কম্পোজিট লিমিটেড", "Legal name"),
            PartnerIdentificationBasis.AuthoritativeDocument,
            [new PartnerIdentifier(PartnerIdentifierKind.TradeLicenceNumber, "RL/RGJ/2022/11884")],
            [Ostagar.Core.CurrencyCode.Bdt],
            PartnerFixture.RegisteredOn,
            banglaName: BanglaName.From("রূপগঞ্জ নিট কম্পোজিট লিমিটেড")));

        Assert.True(partner.LegalName.IsBangla);
        Assert.True(partner.HasBanglaName);
        Assert.Equal("রূপগঞ্জ নিট কম্পোজিট লিমিটেড", partner.BanglaName?.Value);
    }

    [Fact]
    public void Mixed_script_and_local_punctuation_are_still_names()
    {
        var name = PartnerName.From("Rahima Garments (প্রাইভেট) লিমিটেড", "Legal name");

        Assert.Equal("Rahima Garments (প্রাইভেট) লিমিটেড", name.Value);
    }

    [Fact]
    public void Bengali_digits_and_the_taka_sign_survive_validation()
    {
        var name = PartnerName.From("মাদার টেক্স ১৯৮৫ লিমিটেড ৳", "Legal name");

        Assert.Contains("১৯৮৫", name.Value, StringComparison.Ordinal);
    }

    [Fact]
    public void Whitespace_is_collapsed_and_edges_trimmed_rather_than_stored_as_typed()
    {
        var name = PartnerName.From("  Meghna   Apparels\tLimited  ", "Legal name");

        Assert.Equal("Meghna Apparels Limited", name.Value);
    }

    [Fact]
    public void The_Bangla_name_field_refuses_a_transliteration()
    {
        var refusal = Assert.Throws<ArgumentException>(() => BanglaName.From("Sonali Cotton Mills Limited"));

        Assert.Contains("Bangla script", refusal.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("N/A")]
    [InlineData("নেই")]
    [InlineData("1234 5678")]
    [InlineData("<script>alert(1)</script>")]
    [InlineData("Rahima\u0007Garments")]
    public void A_name_has_to_be_a_name(string candidate)
    {
        Assert.ThrowsAny<ArgumentException>(() => PartnerName.From(candidate, "Legal name"));
    }

    [Fact]
    public void A_name_longer_than_a_document_header_can_print_is_refused()
    {
        var tooLong = new string('ক', PartnerTextRules.MaxNameLength + 1);

        Assert.Throws<ArgumentException>(() => PartnerName.From(tooLong, "Legal name"));
    }

    [Fact]
    public void An_identifier_is_not_a_name_and_never_becomes_one()
    {
        Assert.False(PartnerName.TryFrom("4561237890", out _));
        Assert.True(PartnerName.TryFrom("4561237890 Sonali Traders", out var parsed));
        Assert.Equal("4561237890 Sonali Traders", parsed.Value);
    }
}
