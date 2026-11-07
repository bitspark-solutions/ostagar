using Ostagar.Core;
using Ostagar.Modules.Partners;

namespace Ostagar.Tests.Modules.Partners;

/// <summary>
/// Money may only move through a channel that has been written down. These tests hold the module to refusing
/// rather than defaulting, which is the difference between a payment that can be traced to a bank statement
/// and one that cannot.
/// </summary>
public sealed class PaymentChannelTests
{
    [Fact]
    public void Paying_a_partner_with_no_bank_account_is_refused()
    {
        var partner = PartnerFixture.TradeLicencePartner();

        partner.GrantVendorRole(PartnerFixture.VendorTerms());

        Assert.False(partner.HasPaymentChannel);

        var refusal = Assert.Throws<PartnerRuleException>(() => partner.RequirePaymentChannel());

        Assert.Equal(PartnerErrorCode.PaymentChannelMissing, refusal.Code);
    }

    [Fact]
    public void One_recorded_account_is_a_channel_and_is_returned_unasked()
    {
        var partner = PartnerFixture.TradeLicencePartner();

        partner.GrantVendorRole(PartnerFixture.VendorTerms());
        partner.AddBankAccount(PartnerFixture.Account("1122334455667788"));

        var channel = partner.RequirePaymentChannel();

        Assert.Equal("1122334455667788", channel.AccountNumber);
        Assert.Equal(CurrencyCode.Bdt, channel.Currency.Code);
    }

    [Fact]
    public void Several_accounts_with_none_designated_are_refused_rather_than_chosen_by_list_order()
    {
        var partner = PartnerFixture.TradeLicencePartner();

        partner.AddBankAccount(PartnerFixture.Account("1111222233334444"));
        partner.AddBankAccount(PartnerFixture.Account("5555666677778888", bankName: "Dutch-Bangla Bank PLC"));

        var refusal = Assert.Throws<PartnerRuleException>(() => partner.RequirePaymentChannel());

        Assert.Equal(PartnerErrorCode.PaymentChannelAmbiguous, refusal.Code);
    }

    [Fact]
    public void The_designated_account_is_the_one_that_gets_paid()
    {
        var partner = PartnerFixture.TradeLicencePartner();

        var first = PartnerFixture.Account("1111222233334444");
        var second = PartnerFixture.Account("5555666677778888", bankName: "Islami Bank Bangladesh");

        partner.AddBankAccount(first);
        partner.AddBankAccount(second);

        partner.DesignatePaymentChannel(second);

        Assert.Equal("5555666677778888", partner.RequirePaymentChannel().AccountNumber);

        partner.DesignatePaymentChannel(first);

        Assert.Equal("1111222233334444", partner.RequirePaymentChannel().AccountNumber);
        Assert.False(second.IsPreferred);
    }

    [Fact]
    public void An_account_of_another_partner_cannot_be_designated()
    {
        var partner = PartnerFixture.TradeLicencePartner(code: "PRT-8001");
        var other = PartnerFixture.Account("9999888877776666");

        var refusal = Assert.Throws<PartnerRuleException>(() => partner.DesignatePaymentChannel(other));

        Assert.Equal(PartnerErrorCode.PaymentChannelMissing, refusal.Code);
    }

    [Fact]
    public void An_archived_account_is_not_a_channel_but_is_still_readable()
    {
        var partner = PartnerFixture.TradeLicencePartner();

        var account = PartnerFixture.Account("4444333322221111");
        partner.AddBankAccount(account);

        partner.ArchiveBankAccount(account);

        Assert.False(partner.HasPaymentChannel);
        Assert.Throws<PartnerRuleException>(() => partner.RequirePaymentChannel());
        Assert.Equal(account.AccountNumber, Assert.Single(partner.BankAccounts).AccountNumber);

        partner.ReinstateBankAccount(account);

        Assert.Equal("4444333322221111", partner.RequirePaymentChannel().AccountNumber);
    }

    [Fact]
    public void A_channel_cannot_be_designated_once_it_is_archived()
    {
        var partner = PartnerFixture.TradeLicencePartner();

        var account = PartnerFixture.Account("4444333322221111");
        partner.AddBankAccount(account);
        partner.ArchiveBankAccount(account);

        var refusal = Assert.Throws<PartnerRuleException>(() => partner.DesignatePaymentChannel(account));

        Assert.Equal(PartnerErrorCode.PaymentChannelMissing, refusal.Code);
    }

    [Fact]
    public void Two_accounts_both_marked_to_pay_are_refused()
    {
        var partner = PartnerFixture.TradeLicencePartner();

        partner.AddBankAccount(PartnerFixture.Account("1111111111111111", isPreferred: true));

        var refusal = Assert.Throws<PartnerRuleException>(
            () => partner.AddBankAccount(PartnerFixture.Account("2222222222222222", isPreferred: true)));

        Assert.Equal(PartnerErrorCode.PaymentChannelAmbiguous, refusal.Code);
        Assert.Single(partner.BankAccounts);
    }

    [Fact]
    public void Payment_in_a_currency_the_partner_does_not_trade_in_is_refused()
    {
        var partner = PartnerFixture.TradeLicencePartner();

        partner.AddBankAccount(PartnerFixture.Account("3333333333333333", PartnerFixture.Bdt));

        var refusal = Assert.Throws<PartnerRuleException>(
            () => partner.RequirePaymentChannel(CurrencyCode.Usd));

        Assert.Equal(PartnerErrorCode.PaymentCurrencyNotTraded, refusal.Code);
    }

    [Fact]
    public void An_account_in_a_currency_the_partner_is_never_invoiced_in_is_refused_at_entry()
    {
        var partner = PartnerFixture.TradeLicencePartner();

        var refusal = Assert.Throws<PartnerRuleException>(
            () => partner.AddBankAccount(PartnerFixture.Account("7777777777777777", PartnerFixture.Usd)));

        Assert.Equal(PartnerErrorCode.PaymentCurrencyNotTraded, refusal.Code);
    }

    [Fact]
    public void A_settled_partner_can_still_be_paid_while_inactive()
    {
        var partner = PartnerFixture.TradeLicencePartner();

        partner.AddBankAccount(PartnerFixture.Account("8888888888888888", isPreferred: true));
        partner.Deactivate(hasOpenDocuments: false, PartnerFixture.RegisteredOn);

        Assert.False(partner.IsActive);
        Assert.Equal("8888888888888888", partner.RequirePaymentChannel().AccountNumber);
    }

    [Fact]
    public void An_account_added_later_joins_the_duplicate_matching_set()
    {
        var partner = PartnerFixture.TradeLicencePartner();

        partner.AddBankAccount(PartnerFixture.Account("6666555544443333"));

        var matching = partner.IdentificationForMatching();

        Assert.Contains(
            matching,
            identifier => identifier.Kind == PartnerIdentifierKind.BankAccountNumber
                && identifier.MatchKey == "6666555544443333");

        var collision = DuplicateMatchEvaluator.Evaluate(
            [new PartnerIdentifier(PartnerIdentifierKind.BankAccountNumber, "6666-5555-4444-3333")],
            [PartnerFixture.Stored(
                partner.Id,
                partner.Code.Value,
                partner.LegalName.Value,
                PartnerIdentifierKind.BankAccountNumber,
                partner.BankAccounts[0].AccountNumber)]);

        Assert.Equal(DuplicateVerdict.SuspectedDuplicate, collision.Verdict);
    }

    [Fact]
    public void The_same_account_number_cannot_be_recorded_twice_on_one_partner()
    {
        var partner = PartnerFixture.TradeLicencePartner();

        partner.AddBankAccount(PartnerFixture.Account("1234123412341234"));

        var refusal = Assert.Throws<PartnerRuleException>(
            () => partner.AddBankAccount(PartnerFixture.Account("1234 1234 1234 1234")));

        Assert.Equal(PartnerErrorCode.IdentifierDuplicated, refusal.Code);
    }
}
