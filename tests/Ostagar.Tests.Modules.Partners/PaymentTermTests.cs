using Ostagar.Core;
using Ostagar.Modules.Partners;

namespace Ostagar.Tests.Modules.Partners;

/// <summary>
/// Terms decide when money falls due and how much of an order falls due now. The advance forms are where an
/// export factory's real agreements live, and where an unexamined percentage quietly loses a paisa.
/// </summary>
public sealed class PaymentTermTests
{
    private static readonly DateOnly OrderDate = new(2026, 11, 16);

    [Fact]
    public void A_thirty_percent_advance_and_its_balance_re_add_to_the_order_exactly()
    {
        var term = PaymentTerm.ForPartialAdvance(30m, PaymentTerm.ForNetDays(30));
        var order = Money.Bdt(1_234_567.89m);

        var advance = term.AdvanceDue(order);
        var balance = term.BalanceDue(order);

        Assert.Equal(370_370.37m, advance.Amount);
        Assert.Equal(864_197.52m, balance.Amount);
        Assert.Equal(order, Money.Add(advance, balance));
        Assert.Equal(new DateOnly(2026, 12, 16), term.DueOn(OrderDate));
    }

    [Fact]
    public void A_full_advance_is_due_on_the_document_date_and_leaves_no_balance_to_schedule()
    {
        var term = PaymentTerm.ForAdvance();

        Assert.Equal(Money.Bdt(90_500m), term.AdvanceDue(Money.Bdt(90_500m)));
        Assert.True(term.BalanceDue(Money.Bdt(90_500m)).IsZero);
        Assert.Equal(OrderDate, term.DueOn(OrderDate));
        Assert.Null(term.AdvancePercentage);
        Assert.Null(term.Balance);
    }

    [Fact]
    public void Net_days_are_counted_from_the_document_date()
    {
        var term = PaymentTerm.ForNetDays(45);

        Assert.Equal(new DateOnly(2026, 12, 31), term.DueOn(OrderDate));
        Assert.Equal(45, term.NetDays);
        Assert.Null(term.DayOfMonth);
    }

    [Fact]
    public void A_fixed_day_falls_in_the_month_after_the_document_and_rolls_the_year()
    {
        var term = PaymentTerm.ForFixedDayOfMonth(5);

        Assert.Equal(new DateOnly(2026, 12, 5), term.DueOn(new DateOnly(2026, 11, 28)));
        Assert.Equal(new DateOnly(2027, 1, 5), term.DueOn(new DateOnly(2026, 12, 20)));
    }

    [Fact]
    public void A_fixed_day_that_the_following_month_does_not_have_lands_on_its_last_day()
    {
        var term = PaymentTerm.ForFixedDayOfMonth(31);

        Assert.Equal(new DateOnly(2026, 2, 28), term.DueOn(new DateOnly(2026, 1, 15)));
        Assert.Equal(new DateOnly(2026, 4, 30), term.DueOn(new DateOnly(2026, 3, 31)));
    }

    [Fact]
    public void An_advance_of_one_hundred_percent_is_not_a_partial_advance()
    {
        var refusal = Assert.Throws<ArgumentException>(
            () => PaymentTerm.ForPartialAdvance(100m, PaymentTerm.ForNetDays(30)));

        Assert.Contains("ForAdvance", refusal.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void An_advance_of_zero_or_less_is_not_an_advance()
    {
        Assert.Throws<ArgumentException>(() => PaymentTerm.ForPartialAdvance(0m, PaymentTerm.ForNetDays(30)));
        Assert.Throws<ArgumentException>(() => PaymentTerm.ForPartialAdvance(-5m, PaymentTerm.ForNetDays(30)));
    }

    [Fact]
    public void A_percentage_no_currency_can_settle_is_refused()
    {
        Assert.Throws<ArgumentException>(() => PaymentTerm.ForPartialAdvance(33.33333m, PaymentTerm.ForNetDays(30)));

        var exact = PaymentTerm.ForPartialAdvance(33.3333m, PaymentTerm.ForNetDays(30));

        Assert.Equal(33.3333m, exact.AdvancePercentage);
    }

    [Fact]
    public void The_balance_of_a_partial_advance_cannot_itself_be_an_advance()
    {
        var refusal = Assert.Throws<ArgumentException>(
            () => PaymentTerm.ForPartialAdvance(40m, PaymentTerm.ForAdvance()));

        Assert.Contains("advance", refusal.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_net_days_term_outside_a_payment_term_is_refused_rather_than_loosened()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => PaymentTerm.ForNetDays(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => PaymentTerm.ForNetDays(366));
        Assert.Throws<ArgumentOutOfRangeException>(() => PaymentTerm.ForFixedDayOfMonth(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => PaymentTerm.ForFixedDayOfMonth(32));
    }

    [Fact]
    public void A_term_carries_its_own_kind_and_nothing_else()
    {
        Assert.Equal(PaymentTermKind.NetDays, PaymentTerm.ForNetDays(30).Kind);
        Assert.Equal(PaymentTermKind.FixedDayOfMonth, PaymentTerm.ForFixedDayOfMonth(7).Kind);
        Assert.Equal(PaymentTermKind.Advance, PaymentTerm.ForAdvance().Kind);
        Assert.Equal(PaymentTermKind.PartialAdvance, PaymentTerm.ForPartialAdvance(25m, PaymentTerm.ForNetDays(60)).Kind);
    }

    [Fact]
    public void A_term_without_an_advance_leaves_the_whole_order_to_pay_now_as_balance()
    {
        var term = PaymentTerm.ForNetDays(30);

        Assert.True(term.AdvanceDue(Money.Bdt(12_345.67m)).IsZero);
        Assert.Equal(Money.Bdt(12_345.67m), term.BalanceDue(Money.Bdt(12_345.67m)));
    }
}
