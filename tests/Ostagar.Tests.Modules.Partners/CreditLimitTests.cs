using Ostagar.Core;
using Ostagar.Modules.Partners;

namespace Ostagar.Tests.Modules.Partners;

/// <summary>
/// The credit check is exact arithmetic at the currency's own scale, so the boundary is where an owner's
/// decision actually bites. A tolerance here would be the legacy validator's ±0.01 again: one whole minor
/// unit, wide enough to hide a limit that was never honoured.
/// </summary>
public sealed class CreditLimitTests
{
    private static readonly Money Limit = Money.Bdt(500_000m);

    [Fact]
    public void Exposure_plus_order_exactly_equal_to_the_limit_is_approved_with_nothing_to_spare()
    {
        var decision = CreditLimitRule.Evaluate(Limit, Money.Bdt(400_000m), Money.Bdt(100_000m));

        Assert.Equal(CreditDecisionStatus.Approved, decision.Status);
        Assert.True(decision.IsApproved);
        Assert.Equal(0m, decision.Amount?.Amount);
        Assert.Equal(500_000m, decision.Committed?.Amount);
        Assert.Equal(Limit, decision.Limit);
    }

    [Fact]
    public void One_paisa_over_the_limit_is_refused_with_the_overage_stated_exactly()
    {
        var decision = CreditLimitRule.Evaluate(Limit, Money.Bdt(400_000m), Money.Bdt(100_000.01m));

        Assert.Equal(CreditDecisionStatus.OverLimit, decision.Status);
        Assert.Equal(0.01m, decision.Amount?.Amount);
        Assert.Equal(CurrencyCode.Bdt, decision.Amount?.Currency.Code);
        Assert.Equal(500_000.01m, decision.Committed?.Amount);
    }

    [Fact]
    public void The_refusal_reports_the_overage_and_never_a_headroom_clamped_to_zero()
    {
        var decision = CreditLimitRule.Evaluate(Limit, Money.Bdt(700_000m), Money.Bdt(50_000m));

        Assert.Equal(CreditDecisionStatus.OverLimit, decision.Status);
        Assert.Equal(250_000m, decision.Amount?.Amount);
        Assert.False(decision.Amount!.Value.IsNegative);
    }

    [Fact]
    public void No_credit_limit_refuses_the_sale_instead_of_treating_the_customer_as_unlimited()
    {
        var decision = CreditLimitRule.Evaluate(null, Money.Bdt(10_000m), Money.Bdt(5_000m));

        Assert.Equal(CreditDecisionStatus.CreditLimitNotSet, decision.Status);
        Assert.False(decision.IsApproved);
        Assert.Null(decision.Committed);
    }

    [Fact]
    public void A_zero_limit_means_cash_before_dispatch_and_refuses_any_credit_sale()
    {
        var decision = CreditLimitRule.Evaluate(Money.Bdt(0m), Money.Bdt(0m), Money.Bdt(1m));

        Assert.Equal(CreditDecisionStatus.OverLimit, decision.Status);
        Assert.Equal(1m, decision.Amount?.Amount);
    }

    [Fact]
    public void An_order_of_zero_is_not_a_credit_sale_and_is_refused_on_its_own_terms()
    {
        var decision = CreditLimitRule.Evaluate(Limit, Money.Bdt(1_000m), Money.Bdt(0m));

        Assert.Equal(CreditDecisionStatus.OrderValueInvalid, decision.Status);
    }

    [Fact]
    public void An_exposure_in_another_currency_cannot_be_compared_with_the_limit()
    {
        var usdExposure = new Money(PartnerFixture.Usd, 40_000m);

        var decision = CreditLimitRule.Evaluate(Limit, usdExposure, Money.Bdt(1_000m));

        Assert.Equal(CreditDecisionStatus.CurrencyMismatch, decision.Status);
        Assert.Null(decision.Committed);
    }

    [Fact]
    public void A_customer_who_has_paid_ahead_is_not_clamped_at_zero_before_the_next_order()
    {
        var decision = CreditLimitRule.Evaluate(Limit, Money.Bdt(-50_000m), Money.Bdt(550_000m));

        Assert.Equal(CreditDecisionStatus.Approved, decision.Status);
        Assert.Equal(500_000m, decision.Committed?.Amount);
        Assert.Equal(0m, decision.Amount?.Amount);
    }

    [Fact]
    public void The_check_runs_through_the_customer_capability_it_belongs_to()
    {
        var partner = PartnerFixture.TradeLicencePartner();

        partner.GrantCustomerRole(PartnerFixture.CustomerTerms(creditLimit: Limit));

        var decision = partner.EvaluateCredit(Money.Bdt(499_999.99m), Money.Bdt(0.02m));

        Assert.Equal(CreditDecisionStatus.OverLimit, decision.Status);
        Assert.Equal(0.01m, decision.Amount?.Amount);
    }

    [Fact]
    public void A_partner_that_is_not_a_customer_has_no_limit_to_check()
    {
        var partner = PartnerFixture.TradeLicencePartner();

        var refusal = Assert.Throws<PartnerRuleException>(
            () => partner.EvaluateCredit(Money.Bdt(0m), Money.Bdt(1_000m)));

        Assert.Equal(PartnerErrorCode.RoleNotHeld, refusal.Code);
    }

    [Fact]
    public void A_credit_limit_in_a_currency_the_partner_is_not_traded_in_is_refused()
    {
        var partner = PartnerFixture.TradeLicencePartner();

        var refusal = Assert.Throws<PartnerRuleException>(
            () => partner.GrantCustomerRole(PartnerFixture.CustomerTerms(creditLimit: new Money(PartnerFixture.Usd, 10_000m))));

        Assert.Equal(PartnerErrorCode.CreditLimitCurrencyNotTraded, refusal.Code);
    }

    [Fact]
    public void A_second_grant_of_the_customer_role_is_refused_rather_than_replacing_terms_silently()
    {
        var partner = PartnerFixture.TradeLicencePartner();

        partner.GrantCustomerRole(PartnerFixture.CustomerTerms(creditLimit: Limit));

        var refusal = Assert.Throws<PartnerRuleException>(
            () => partner.GrantCustomerRole(PartnerFixture.CustomerTerms(receivableCode: "1031", creditLimit: Money.Bdt(10_000m))));

        Assert.Equal(PartnerErrorCode.RoleAlreadyHeld, refusal.Code);
        Assert.Equal(Limit, partner.Customer?.CreditLimit);
    }

    [Fact]
    public void A_negative_credit_limit_is_refused_at_entry_rather_than_read_as_an_overage()
    {
        Assert.Throws<ArgumentException>(() => PartnerFixture.CustomerTerms(creditLimit: Money.Bdt(-1m)));
    }

    [Fact]
    public void A_dollar_trading_partner_may_hold_a_dollar_limit()
    {
        var partner = PartnerFixture.TradeLicencePartner(currencies: [CurrencyCode.Bdt, CurrencyCode.Usd]);

        var usdLimit = new Money(PartnerFixture.Usd, 20_000m);

        partner.GrantCustomerRole(PartnerFixture.CustomerTerms(creditLimit: usdLimit));

        var decision = partner.EvaluateCredit(
            new Money(PartnerFixture.Usd, 15_000m),
            new Money(PartnerFixture.Usd, 5_000m));

        Assert.Equal(CreditDecisionStatus.Approved, decision.Status);
        Assert.Equal(0m, decision.Amount?.Amount);
    }
}
