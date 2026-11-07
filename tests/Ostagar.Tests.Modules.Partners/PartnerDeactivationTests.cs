using Ostagar.Core;
using Ostagar.Modules.Partners;

namespace Ostagar.Tests.Modules.Partners;

/// <summary>
/// Deactivating a partner closes it to new business. It never removes it, and it refuses while documents are
/// still in flight, because a document that names a party the product treats as closed is the orphaning defect
/// this flag exists to avoid.
/// </summary>
public sealed class PartnerDeactivationTests
{
    [Fact]
    public void Deactivating_a_partner_with_an_open_order_is_refused()
    {
        var partner = PartnerFixture.TradeLicencePartner();

        partner.GrantCustomerRole(PartnerFixture.CustomerTerms());

        var refusal = Assert.Throws<PartnerRuleException>(
            () => partner.Deactivate(hasOpenDocuments: true, PartnerFixture.RegisteredOn, "SO-2026-0007"));

        Assert.Equal(PartnerErrorCode.OpenDocumentsExist, refusal.Code);
        Assert.Equal(PartnerStatus.Active, partner.Status);
        Assert.Null(partner.InactiveOn);
        Assert.Contains("SO-2026-0007", refusal.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Withdrawing_a_role_under_an_open_document_is_refused_for_the_same_reason()
    {
        var partner = PartnerFixture.TradeLicencePartner();

        partner.GrantCustomerRole(PartnerFixture.CustomerTerms());
        partner.GrantVendorRole(PartnerFixture.VendorTerms());

        Assert.Throws<PartnerRuleException>(
            () => partner.WithdrawRole(PartnerRole.Customer, hasOpenDocuments: true, "SO-2026-0007"));

        Assert.True(partner.HasRole(PartnerRole.Customer));

        partner.WithdrawRole(PartnerRole.Customer, hasOpenDocuments: false);

        Assert.False(partner.HasRole(PartnerRole.Customer));
        Assert.True(partner.HasRole(PartnerRole.Vendor));
    }

    [Fact]
    public void An_inactive_partner_takes_no_new_business()
    {
        var partner = PartnerFixture.TradeLicencePartner();

        partner.GrantCustomerRole(PartnerFixture.CustomerTerms());

        var style = PartnerFixture.NewStyle();
        partner.IssuePriceList(
            PriceListDirection.Sales,
            PartnerFixture.Bdt,
            PartnerFixture.Today,
            PartnerFixture.Today.AddMonths(2),
            [PartnerFixture.Line(style, 420m)]);

        partner.Deactivate(hasOpenDocuments: false, PartnerFixture.RegisteredOn);

        Assert.Equal(PartnerErrorCode.PartnerInactive, Assert.Throws<PartnerRuleException>(
            () => partner.RequirePrice(style, PriceListDirection.Sales, CurrencyCode.Bdt, PartnerFixture.Today)).Code);

        Assert.Equal(PartnerErrorCode.PartnerInactive, Assert.Throws<PartnerRuleException>(
            () => partner.AddIdentifier(new PartnerIdentifier(PartnerIdentifierKind.TaxIdentificationNumber, "55551234"))).Code);

        Assert.Equal(PartnerErrorCode.PartnerInactive, Assert.Throws<PartnerRuleException>(
            () => partner.AddBankAccount(PartnerFixture.Account("1313131313131313"))).Code);

        Assert.Equal(PartnerErrorCode.PartnerInactive, Assert.Throws<PartnerRuleException>(
            () => partner.SetDocumentCurrencies([CurrencyCode.Bdt, CurrencyCode.Usd], PartnerFixture.Today)).Code);
    }

    [Fact]
    public void Deactivating_a_second_time_reports_that_nothing_changed()
    {
        var partner = PartnerFixture.TradeLicencePartner();

        partner.Deactivate(hasOpenDocuments: false, PartnerFixture.RegisteredOn);

        var refusal = Assert.Throws<PartnerRuleException>(
            () => partner.Deactivate(hasOpenDocuments: false, PartnerFixture.RegisteredOn.AddMinutes(1)));

        Assert.Equal(PartnerErrorCode.PartnerAlreadyInactive, refusal.Code);
    }

    [Fact]
    public void An_inactive_partner_is_not_deleted_and_keeps_everything_a_document_refers_to()
    {
        var partner = PartnerFixture.TradeLicencePartner(legalName: "Bengal Handloom Limited");

        partner.GrantVendorRole(PartnerFixture.VendorTerms());

        var account = PartnerFixture.Account("2727272727272727", isPreferred: true);
        partner.AddBankAccount(account);

        var identity = partner.Id;
        var code = partner.Code;

        partner.Deactivate(hasOpenDocuments: false, PartnerFixture.RegisteredOn);

        Assert.Equal(identity, partner.Id);
        Assert.Equal(code, partner.Code);
        Assert.Equal("Bengal Handloom Limited", partner.LegalName.Value);
        Assert.Equal(2, partner.Identifiers.Count);
        Assert.Equal(account.AccountNumber, Assert.Single(partner.BankAccounts).AccountNumber);
        Assert.Equal("2727272727272727", partner.RequirePaymentChannel().AccountNumber);
        Assert.Equal(PartnerRole.Vendor, Assert.Single(partner.Roles));
        Assert.NotNull(partner.InactiveOn);
    }

    [Fact]
    public void Reactivating_needs_no_restoration_because_nothing_was_lost()
    {
        var partner = PartnerFixture.TradeLicencePartner();

        partner.Deactivate(hasOpenDocuments: false, PartnerFixture.RegisteredOn);
        partner.Reactivate();

        Assert.True(partner.IsActive);
        Assert.Null(partner.InactiveOn);

        Assert.Equal(PartnerErrorCode.PartnerAlreadyActive, Assert.Throws<PartnerRuleException>(
            () => partner.Reactivate()).Code);
    }

    [Fact]
    public void The_inactive_state_is_named_in_both_languages_for_the_screen_that_shows_it()
    {
        var english = PartnerVocabulary.Label(PartnerStatus.Inactive);

        Assert.Equal("Inactive", english.English);
        Assert.Equal("নিষ্ক্রিয়", english.Bangla);
    }
}
