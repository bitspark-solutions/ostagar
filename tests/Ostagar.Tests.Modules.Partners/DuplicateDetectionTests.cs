using Ostagar.Modules.Partners;

namespace Ostagar.Tests.Modules.Partners;

/// <summary>
/// Two records for one business corrupt an aged-receivables report silently, so the duplicate rule is the
/// place where the product is allowed to be loud. These tests fix which identifiers carry that weight, and
/// which do not.
/// </summary>
public sealed class DuplicateDetectionTests
{
    [Fact]
    public void The_same_tin_on_two_records_is_a_confirmed_duplicate()
    {
        var existing = PartnerFixture.Stored(
            PartnerId.New(),
            "PRT-1001",
            "Sonali Cotton Mills Limited",
            PartnerIdentifierKind.TaxIdentificationNumber,
            "456-123-7890");

        var report = DuplicateMatchEvaluator.Evaluate(
            [new PartnerIdentifier(PartnerIdentifierKind.TaxIdentificationNumber, "4561237890")],
            [existing]);

        Assert.Equal(DuplicateVerdict.ConfirmedDuplicate, report.Verdict);
        Assert.True(DuplicateMatchEvaluator.BlocksCreation(report));
        Assert.Equal(existing.PartnerId, Assert.Single(report.Matches).ExistingPartner);
        Assert.Equal(IdentifierAuthority.Authoritative, report.Matches[0].Authority);
    }

    [Fact]
    public void An_identical_name_with_no_shared_identifier_is_not_a_duplicate()
    {
        var existing = PartnerFixture.Stored(
            PartnerId.New(),
            "PRT-2200",
            "Rahima Garments",
            PartnerIdentifierKind.TradeLicenceNumber,
            "RL/CTK/2019/00111");

        var report = DuplicateMatchEvaluator.Evaluate(
            [new PartnerIdentifier(PartnerIdentifierKind.TradeLicenceNumber, "RL/CTK/2019/00999")],
            [existing]);

        Assert.Equal(DuplicateVerdict.Unique, report.Verdict);
        Assert.Empty(report.Matches);
    }

    [Fact]
    public void A_shared_phone_number_raises_a_review_and_does_not_block()
    {
        var existing = PartnerFixture.Stored(
            PartnerId.New(),
            "PRT-2201",
            "Rahima Traders",
            PartnerIdentifierKind.PhoneNumber,
            "01711-123456");

        var report = DuplicateMatchEvaluator.Evaluate(
            [new PartnerIdentifier(PartnerIdentifierKind.PhoneNumber, "01711 123456")],
            [existing]);

        Assert.Equal(DuplicateVerdict.SuspectedDuplicate, report.Verdict);
        Assert.False(DuplicateMatchEvaluator.BlocksCreation(report));
        Assert.Equal(PartnerIdentifierKind.PhoneNumber, report.Matches[0].Kind);
    }

    [Fact]
    public void A_shared_bank_account_is_evidence_worth_checking_but_not_enough_to_refuse()
    {
        var existing = PartnerFixture.Stored(
            PartnerId.New(),
            "PRT-2202",
            "Rupganj Textiles",
            PartnerIdentifierKind.BankAccountNumber,
            "1234 5678 9012");

        var report = DuplicateMatchEvaluator.Evaluate(
            [new PartnerIdentifier(PartnerIdentifierKind.BankAccountNumber, "123456789012")],
            [existing]);

        Assert.Equal(DuplicateVerdict.SuspectedDuplicate, report.Verdict);
        Assert.Equal(IdentifierAuthority.Suggestive, report.Matches[0].Authority);
    }

    [Fact]
    public void An_authoritative_match_outranks_a_run_of_suggestive_ones()
    {
        var phoneOwner = PartnerFixture.Stored(
            PartnerId.New(),
            "PRT-2203",
            "Meghna Apparels",
            PartnerIdentifierKind.PhoneNumber,
            "01811223344");
        var emailOwner = PartnerFixture.Stored(
            PartnerId.New(),
            "PRT-2204",
            "Meghna Fashion",
            PartnerIdentifierKind.EmailAddress,
            "info@meghna.com.bd");
        var tinOwner = PartnerFixture.Stored(
            PartnerId.New(),
            "PRT-2205",
            "Meghna Group",
            PartnerIdentifierKind.TaxIdentificationNumber,
            "7788991100");

        var report = DuplicateMatchEvaluator.Evaluate(
            [
                new PartnerIdentifier(PartnerIdentifierKind.PhoneNumber, "01811 223344"),
                new PartnerIdentifier(PartnerIdentifierKind.EmailAddress, "info@meghna.com.bd"),
                new PartnerIdentifier(PartnerIdentifierKind.VatRegistrationNumber, "7788991100"),
                new PartnerIdentifier(PartnerIdentifierKind.TaxIdentificationNumber, "7788991100"),
            ],
            [phoneOwner, emailOwner, tinOwner]);

        Assert.Equal(DuplicateVerdict.ConfirmedDuplicate, report.Verdict);
        Assert.Equal(3, report.Matches.Count);
        Assert.Equal(IdentifierAuthority.Authoritative, report.Matches[0].Authority);
        Assert.Equal(3, report.ConflictingPartners.Count);
    }

    [Fact]
    public void A_partner_is_never_its_own_duplicate()
    {
        var partner = PartnerFixture.TradeLicencePartner();
        var account = PartnerFixture.Account("9988776655443322");

        var stored = partner.IdentificationForMatching()
            .Select(identifier => PartnerFixture.Stored(
                partner.Id, partner.Code.Value, partner.LegalName.Value, identifier.Kind, identifier.Value))
            .ToList();

        partner.AddBankAccount(account);

        var report = DuplicateMatchEvaluator.Evaluate(
            partner.IdentificationForMatching(),
            stored,
            excludePartnerId: partner.Id);

        Assert.Equal(DuplicateVerdict.Unique, report.Verdict);

        var againstAnotherPartner = DuplicateMatchEvaluator.Evaluate(
            partner.IdentificationForMatching(),
            stored);

        Assert.Equal(DuplicateVerdict.ConfirmedDuplicate, againstAnotherPartner.Verdict);
    }

    [Fact]
    public void A_number_from_one_register_does_not_match_the_same_number_in_another()
    {
        var existing = PartnerFixture.Stored(
            PartnerId.New(),
            "PRT-2206",
            "Titas Garments",
            PartnerIdentifierKind.TaxIdentificationNumber,
            "1122334455");

        var report = DuplicateMatchEvaluator.Evaluate(
            [new PartnerIdentifier(PartnerIdentifierKind.VatRegistrationNumber, "1122334455")],
            [existing]);

        Assert.Equal(DuplicateVerdict.Unique, report.Verdict);
    }

    [Fact]
    public void Identifiers_are_compared_in_normalised_form_not_as_typed()
    {
        var existing = PartnerFixture.Stored(
            PartnerId.New(),
            "PRT-2207",
            "Padma Fabrics",
            PartnerIdentifierKind.TradeLicenceNumber,
            "RL / CTK / 2019 / 00456");

        var report = DuplicateMatchEvaluator.Evaluate(
            [new PartnerIdentifier(PartnerIdentifierKind.TradeLicenceNumber, "rl-ctk-2019-00456")],
            [existing]);

        Assert.Equal(DuplicateVerdict.ConfirmedDuplicate, report.Verdict);
    }

    [Theory]
    [InlineData(PartnerIdentifierKind.TaxIdentificationNumber, "N/A")]
    [InlineData(PartnerIdentifierKind.TaxIdentificationNumber, "0000")]
    [InlineData(PartnerIdentifierKind.TradeLicenceNumber, "UNKNOWN")]
    [InlineData(PartnerIdentifierKind.BankAccountNumber, "12 34")]
    [InlineData(PartnerIdentifierKind.EmailAddress, "not-an-email")]
    [InlineData(PartnerIdentifierKind.PhoneNumber, "+88-017-abc")]
    public void A_value_that_is_not_an_identifier_is_refused_at_entry(PartnerIdentifierKind kind, string value)
    {
        Assert.Throws<ArgumentException>(() => new PartnerIdentifier(kind, value));
    }

    [Fact]
    public void A_placeholder_never_becomes_the_identifier_that_matches_every_partner()
    {
        Assert.Throws<ArgumentException>(() => new PartnerIdentifier(PartnerIdentifierKind.TradeLicenceNumber, "n/a"));
    }
}
