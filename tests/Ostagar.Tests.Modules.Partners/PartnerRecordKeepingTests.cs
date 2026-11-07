using Ostagar.Core;
using Ostagar.Modules.Partners;

namespace Ostagar.Tests.Modules.Partners;

/// <summary>
/// The sub-records a partner owns, and the rules that keep them from becoming a second source of truth: one
/// registered address, one primary channel of each kind, one designated account, and an identifier set that
/// never drops below the evidence the record was opened on.
/// </summary>
public sealed class PartnerRecordKeepingTests
{
    [Fact]
    public void An_identifier_cannot_be_recorded_twice_on_one_partner()
    {
        var partner = PartnerFixture.TradeLicencePartner();

        var refusal = Assert.Throws<PartnerRuleException>(
            () => partner.AddIdentifier(new PartnerIdentifier(PartnerIdentifierKind.TaxIdentificationNumber, "456-123-7890")));

        Assert.Equal(PartnerErrorCode.IdentifierDuplicated, refusal.Code);
    }

    [Fact]
    public void An_identifier_that_is_not_there_cannot_be_removed()
    {
        var partner = PartnerFixture.TradeLicencePartner();

        var refusal = Assert.Throws<PartnerRuleException>(
            () => partner.RemoveIdentifier(new PartnerIdentifier(PartnerIdentifierKind.VatRegistrationNumber, "999888777666")));

        Assert.Equal(PartnerErrorCode.IdentifierNotHeld, refusal.Code);
    }

    [Fact]
    public void One_authoritative_identifier_may_be_replaced_but_never_losing_the_last()
    {
        var partner = PartnerFixture.TradeLicencePartner();

        // Replacing one authoritative identifier with another is allowed, and the aggregate must not
        // refuse it just because the count would momentarily depend on the new one.
        partner.AddIdentifier(new PartnerIdentifier(PartnerIdentifierKind.VatRegistrationNumber, "0258963147001"));

        partner.RemoveIdentifier(new PartnerIdentifier(PartnerIdentifierKind.TaxIdentificationNumber, "4561237890"));

        // Removing the trade licence now succeeds, and that is the correct answer: this module declares
        // trade licence, e-TIN, VAT registration and ERC all authoritative, so a VAT number really is a
        // replacement rather than a downgrade. The original assertion assumed VAT was only suggestive
        // evidence, which contradicts PartnerIdentifierKind's own classification.
        partner.RemoveIdentifier(new PartnerIdentifier(PartnerIdentifierKind.TradeLicenceNumber, "RL/CTK/2019/00456"));

        Assert.Single(partner.Identifiers);

        // Now there is exactly one authoritative identifier left, and losing it is what the rule protects.
        var refusal = Assert.Throws<PartnerRuleException>(
            () => partner.RemoveIdentifier(new PartnerIdentifier(PartnerIdentifierKind.VatRegistrationNumber, "0258963147001")));

        Assert.Equal(PartnerErrorCode.AuthoritativeIdentifierRequired, refusal.Code);
        Assert.Contains(partner.Identifiers, identifier => identifier.Kind == PartnerIdentifierKind.VatRegistrationNumber);
    }

    [Fact]
    public void A_partner_on_secondary_evidence_cannot_drop_to_a_single_suggestive_identifier()
    {
        var partner = PartnerFixture.SecondaryEvidencePartner();

        var refusal = Assert.Throws<PartnerRuleException>(
            () => partner.RemoveIdentifier(new PartnerIdentifier(PartnerIdentifierKind.PhoneNumber, "01711 123456")));

        Assert.Equal(PartnerErrorCode.IdentificationEvidenceInsufficient, refusal.Code);

        partner.AddIdentifier(new PartnerIdentifier(PartnerIdentifierKind.BankAccountNumber, "1111000022223333"));

        partner.RemoveIdentifier(new PartnerIdentifier(PartnerIdentifierKind.EmailAddress, "rahima.traders@gmail.com"));

        Assert.Equal(2, partner.Identifiers.Count);
    }

    [Fact]
    public void Only_one_address_may_claim_to_be_the_registered_one()
    {
        var partner = PartnerFixture.TradeLicencePartner();

        var refusal = Assert.Throws<PartnerRuleException>(
            () => partner.AddAddress(new PartnerAddress(AddressKind.Registered, "Second factory gate", "Dhaka")));

        // RegisteredAddressRequired means "there is none". The situation here is the opposite — there is
        // already one — and accepting the wrong code would let the two distinct rules look satisfied by
        // each other.
        Assert.Equal(PartnerErrorCode.AddressClaimDuplicated, refusal.Code);

        partner.AddAddress(new PartnerAddress(AddressKind.Delivery, "Bond warehouse, Agrabad", "Chattogram"));
        partner.AddAddress(new PartnerAddress(AddressKind.Delivery, "Plot 8, Export Processing Zone", "Narayanganj"));

        Assert.Equal(3, partner.Addresses.Count);
    }

    [Fact]
    public void Only_one_contact_per_kind_may_be_the_primary_one()
    {
        var partner = PartnerFixture.TradeLicencePartner();

        partner.AddContactPoint(new PartnerContactPoint(ContactKind.Mobile, "+880 1711 123456", attention: "Merchandiser", isPrimary: true));

        var refusal = Assert.Throws<PartnerRuleException>(
            () => partner.AddContactPoint(new PartnerContactPoint(ContactKind.Mobile, "+880 1811 223344", isPrimary: true)));

        Assert.Equal(PartnerErrorCode.PaymentChannelAmbiguous, refusal.Code);

        partner.AddContactPoint(new PartnerContactPoint(ContactKind.WhatsApp, "+880 1911 334455", attention: "Export section"));

        Assert.Equal(2, partner.ContactPoints.Count);
    }

    [Fact]
    public void A_contact_channel_joins_the_identifiers_a_partner_is_matched_by()
    {
        var partner = PartnerFixture.TradeLicencePartner();

        partner.AddContactPoint(new PartnerContactPoint(ContactKind.Email, "Books@Sonali-Cotton.com"));

        var matching = partner.IdentificationForMatching();

        Assert.Contains(
            matching,
            identifier => identifier.Kind == PartnerIdentifierKind.EmailAddress
                && identifier.Value == "books@sonali-cotton.com");

        var collision = DuplicateMatchEvaluator.Evaluate(
            [new PartnerIdentifier(PartnerIdentifierKind.EmailAddress, "books@sonali-cotton.com")],
            [PartnerFixture.Stored(
                partner.Id,
                partner.Code.Value,
                partner.LegalName.Value,
                PartnerIdentifierKind.EmailAddress,
                "books@sonali-cotton.com")]);

        Assert.Equal(DuplicateVerdict.SuspectedDuplicate, collision.Verdict);
    }

    [Fact]
    public void An_unnormalised_phone_is_stored_in_its_comparable_form()
    {
        var partner = PartnerFixture.TradeLicencePartner();

        partner.AddContactPoint(new PartnerContactPoint(ContactKind.Landline, "+880-2-5500 1234"));

        Assert.Equal("+880255001234", Assert.Single(partner.ContactPoints).Value);
    }

    [Fact]
    public void A_partner_carries_a_default_term_and_having_none_is_a_stated_absence()
    {
        var partner = PartnerFixture.TradeLicencePartner();

        var term = PaymentTerm.ForPartialAdvance(30m, PaymentTerm.ForNetDays(45));

        partner.GrantCustomerRole(PartnerFixture.CustomerTerms(creditLimit: Money.Bdt(500_000m), term: term));
        partner.GrantVendorRole(PartnerFixture.VendorTerms(term: PaymentTerm.ForAdvance()));

        Assert.Equal(term, partner.Customer?.DefaultPaymentTerm);
        Assert.Equal(PaymentTermKind.Advance, partner.Vendor?.DefaultPaymentTerm?.Kind);
        // The balance falls due on the inner term's own clock: 45 days from 16 November is 31 December.
        // The expected value here was originally 45 + 30 = 75 days, which reads the 30 as a day count
        // when it is an advance PERCENTAGE — a category error that would have made every part-advance
        // invoice due a month late.
        Assert.Equal(new DateOnly(2026, 12, 31), term.DueOn(new DateOnly(2026, 11, 16)));
        Assert.Equal(new DateOnly(2027, 1, 15), PaymentTerm.ForNetDays(60).DueOn(new DateOnly(2026, 11, 16)));

        // A term of "none" is a stated absence on the profile, not an absent field on the role — and the
        // aggregate refuses granting vendor to a partner who is already a vendor, so there is nothing to
        // re-grant here. The earlier call to GrantVendorRole above was left over from an abandoned edit
        // and asserted nothing about the rule this line is about.
        Assert.Null(PartnerFixture.VendorTerms().DefaultPaymentTerm);
    }

    [Fact]
    public void No_currency_is_inferred_for_a_partner_from_the_tenant_default()
    {
        var refusal = Assert.Throws<PartnerRuleException>(() => Partner.Register(new NewPartner(
            PartnerFixture.Tenant,
            PartnerId.New(),
            PartnerCode.From("PRT-9200"),
            PartnerName.From("Karnaphuli Knits", "Legal name"),
            PartnerIdentificationBasis.AuthoritativeDocument,
            [new PartnerIdentifier(PartnerIdentifierKind.TaxIdentificationNumber, "1122334455")],
            [],
            PartnerFixture.RegisteredOn)));

        Assert.Equal(PartnerErrorCode.DocumentCurrencyRequired, refusal.Code);
    }
}
