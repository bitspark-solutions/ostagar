using Ostagar.Core;
using Ostagar.Ledger;
using Ostagar.Modules.Partners;

namespace Ostagar.Tests.Modules.Partners;

/// <summary>
/// The rule that a trading house which buys our output and supplies our fabric is one record, and that the
/// ledger still reads two separate sides from it.
/// </summary>
/// <remarks>
/// This is the case the module exists for. If a dual-role partner resolved both capabilities to one account,
/// the receivable and the payable would net silently and the aged reports would each show a fraction of the
/// truth — which is worse than an error, because nothing on screen says the figure is partial.
/// </remarks>
public sealed class DualRolePartnerTests
{
    [Fact]
    public void One_partner_holding_both_roles_resolves_each_side_to_its_own_account()
    {
        var partner = PartnerFixture.TradeLicencePartner();

        partner.GrantCustomerRole(PartnerFixture.CustomerTerms("1030", Money.Bdt(500_000m)));
        partner.GrantVendorRole(PartnerFixture.VendorTerms("2010"));

        Assert.Equal(2, partner.Roles.Count);
        Assert.Equal(PartnerRole.Customer, partner.Roles[0]);
        Assert.Equal(PartnerRole.Vendor, partner.Roles[1]);

        var receivable = partner.AccountFor(PartnerRole.Customer);
        var payable = partner.AccountFor(PartnerRole.Vendor);

        Assert.Equal(AccountSubType.Receivable, receivable.SubType);
        Assert.Equal(AccountSubType.Payable, payable.SubType);
        Assert.Equal(AccountType.Asset, receivable.AccountType);
        Assert.Equal(AccountType.Liability, payable.AccountType);
        Assert.Equal(NormalBalance.Debit, receivable.Side);
        Assert.Equal(NormalBalance.Credit, payable.Side);
        Assert.NotEqual(receivable.Code, payable.Code);
    }

    [Fact]
    public void Both_roles_are_the_same_identity_and_one_code()
    {
        var partner = PartnerFixture.TradeLicencePartner(code: "PRT-4400");

        partner.GrantCustomerRole(PartnerFixture.CustomerTerms());
        partner.GrantVendorRole(PartnerFixture.VendorTerms());

        Assert.Equal(PartnerRole.Customer, partner.Roles[0]);
        Assert.False(partner.Id.IsEmpty);

        var before = partner.Id;
        var beforeCode = partner.Code;

        partner.ChangeLegalName(PartnerName.From("Sonali Cotton and Exports Limited", "Legal name"));

        Assert.Equal(before, partner.Id);
        Assert.Equal(beforeCode, partner.Code);
    }

    [Fact]
    public void One_ledger_account_cannot_serve_both_sides()
    {
        var partner = PartnerFixture.TradeLicencePartner();

        partner.GrantCustomerRole(PartnerFixture.CustomerTerms("1030"));

        var refusal = Assert.Throws<PartnerRuleException>(
            () => partner.GrantVendorRole(PartnerFixture.VendorTerms("1030")));

        Assert.Equal(PartnerErrorCode.TradeAccountConflict, refusal.Code);
    }

    [Fact]
    public void A_customer_needs_a_registered_address_before_it_can_be_invoiced()
    {
        var partner = Partner.Register(new NewPartner(
            PartnerFixture.Tenant,
            PartnerId.New(),
            PartnerCode.From("PRT-4401"),
            PartnerName.From("Karim Knitwear", "Legal name"),
            PartnerIdentificationBasis.AuthoritativeDocument,
            [new PartnerIdentifier(PartnerIdentifierKind.TaxIdentificationNumber, "9876543210")],
            [CurrencyCode.Bdt],
            PartnerFixture.RegisteredOn));

        var refusal = Assert.Throws<PartnerRuleException>(
            () => partner.GrantCustomerRole(PartnerFixture.CustomerTerms()));

        Assert.Equal(PartnerErrorCode.RegisteredAddressRequired, refusal.Code);
    }

    [Fact]
    public void A_partner_without_an_authoritative_identifier_cannot_be_given_credit()
    {
        var partner = PartnerFixture.SecondaryEvidencePartner();

        partner.GrantVendorRole(PartnerFixture.VendorTerms());

        var refusal = Assert.Throws<PartnerRuleException>(
            () => partner.GrantCustomerRole(PartnerFixture.CustomerTerms(creditLimit: Money.Bdt(200_000m))));

        Assert.Equal(PartnerErrorCode.UnverifiedIdentityCannotHoldCredit, refusal.Code);

        partner.AddIdentifier(new PartnerIdentifier(PartnerIdentifierKind.TradeLicenceNumber, "TRR/DAH/2021/77881"));

        Assert.True(partner.IsIdentityVerified);

        partner.GrantCustomerRole(PartnerFixture.CustomerTerms(creditLimit: Money.Bdt(200_000m)));

        Assert.NotNull(partner.Customer);
    }

    [Fact]
    public void A_partner_cannot_be_created_on_a_name_alone()
    {
        var draft = new NewPartner(
            PartnerFixture.Tenant,
            PartnerId.New(),
            PartnerCode.From("PRT-4402"),
            PartnerName.From("New Party", "Legal name"),
            PartnerIdentificationBasis.AuthoritativeDocument,
            [],
            [CurrencyCode.Bdt],
            PartnerFixture.RegisteredOn);

        var refusal = Assert.Throws<PartnerRuleException>(() => Partner.Register(draft));

        Assert.Equal(PartnerErrorCode.IdentificationEvidenceInsufficient, refusal.Code);
    }

    [Fact]
    public void A_partner_on_secondary_evidence_needs_two_independent_identifiers()
    {
        var draft = new NewPartner(
            PartnerFixture.Tenant,
            PartnerId.New(),
            PartnerCode.From("PRT-4403"),
            PartnerName.From("One Number Traders", "Legal name"),
            PartnerIdentificationBasis.SecondaryEvidence,
            [new PartnerIdentifier(PartnerIdentifierKind.PhoneNumber, "01712 345678")],
            [CurrencyCode.Bdt],
            PartnerFixture.RegisteredOn);

        var refusal = Assert.Throws<PartnerRuleException>(() => Partner.Register(draft));

        Assert.Equal(PartnerErrorCode.IdentificationEvidenceInsufficient, refusal.Code);
    }

    [Fact]
    public void The_role_a_price_list_needs_is_the_role_the_direction_belongs_to()
    {
        var partner = PartnerFixture.TradeLicencePartner();

        partner.GrantVendorRole(PartnerFixture.VendorTerms());

        var refusal = Assert.Throws<PartnerRuleException>(() => partner.IssuePriceList(
            PriceListDirection.Sales,
            PartnerFixture.Bdt,
            PartnerFixture.Today,
            PartnerFixture.Today.AddMonths(3),
            [PartnerFixture.Line(PartnerFixture.NewStyle(), 420m)]));

        Assert.Equal(PartnerErrorCode.RoleNotHeld, refusal.Code);
        Assert.Throws<PartnerRuleException>(() => partner.AccountFor(PartnerRole.Customer));
    }

    [Fact]
    public void Withdrawing_a_role_keeps_the_price_lists_that_were_agreed()
    {
        var partner = PartnerFixture.TradeLicencePartner();

        partner.GrantCustomerRole(PartnerFixture.CustomerTerms());

        var style = PartnerFixture.NewStyle();
        partner.IssuePriceList(
            PriceListDirection.Sales,
            PartnerFixture.Bdt,
            PartnerFixture.Today,
            PartnerFixture.Today.AddMonths(3),
            [PartnerFixture.Line(style, 420m)]);

        partner.WithdrawRole(PartnerRole.Customer, hasOpenDocuments: false);

        Assert.False(partner.HasRole(PartnerRole.Customer));
        Assert.Single(partner.PriceLists);
        Assert.Throws<PartnerRuleException>(() => partner.RequirePrice(
            style,
            PriceListDirection.Sales,
            CurrencyCode.Bdt,
            PartnerFixture.Today));
    }
}
