using Ostagar.Core;
using Ostagar.Ledger;

namespace Ostagar.Tests.Unit;

public class PostingBalanceTests
{
    private static readonly Guid Cash = Guid.Parse("11111111-0000-0000-0000-000000000001");
    private static readonly Guid Income = Guid.Parse("22222222-0000-0000-0000-000000000002");
    private static readonly Guid Rounding = Guid.Parse("33333333-0000-0000-0000-000000000003");

    private static PostingLine Debit(Guid account, decimal amount) =>
        new() { AccountId = account, Debit = Money.Bdt(amount), AmountInCurrency = amount };

    private static PostingLine Credit(Guid account, decimal amount) =>
        new() { AccountId = account, Credit = Money.Bdt(amount), AmountInCurrency = amount };

    [Fact]
    public void A_balanced_two_line_posting_passes()
    {
        var result = PostingBalance.Check([Debit(Cash, 500m), Credit(Income, 500m)]);

        Assert.True(result.Succeeded);
        Assert.Empty(result.Errors);
        Assert.Equal(500m, result.TotalDebit);
        Assert.Equal(500m, result.TotalCredit);
    }

    [Fact]
    public void An_empty_posting_is_refused()
    {
        var result = PostingBalance.Check([]);

        Assert.False(result.Succeeded);
        Assert.Equal(PostingErrorCode.NoLines, result.Errors[0].Code);
    }

    [Fact]
    public void A_single_sided_line_is_refused()
    {
        // The legacy cash book wrote exactly this: one line, status Posted, no balance check. It is the
        // root cause of a trial balance that could never balance.
        var result = PostingBalance.Check([Credit(Income, 500m)]);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, error => error.Code == PostingErrorCode.SingleSided);
    }

    [Fact]
    public void A_one_paisa_imbalance_is_refused_because_there_is_no_tolerance()
    {
        // The old validator allowed ±0.01, which is one whole minor unit — wide enough to hide a
        // rounding policy that did not exist.
        var result = PostingBalance.Check([Debit(Cash, 500m), Credit(Income, 499.99m)]);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, error => error.Code == PostingErrorCode.Unbalanced);
        Assert.Equal(0.01m, result.RoundingResidual);
    }

    [Fact]
    public void A_line_with_both_sides_and_a_zero_amount_are_refused()
    {
        var both = new PostingLine
        {
            AccountId = Cash,
            Debit = Money.Bdt(100m),
            Credit = Money.Bdt(100m),
        };

        var zero = new PostingLine { AccountId = Income, Credit = Money.Bdt(0m) };

        var result = PostingBalance.Check([both, zero]);

        Assert.Contains(result.Errors, error => error.Code == PostingErrorCode.LineSides);
        Assert.Contains(result.Errors, error => error.Code == PostingErrorCode.ZeroAmount);
    }

    [Fact]
    public void Two_functional_currencies_in_one_posting_are_refused()
    {
        var dollars = new PostingLine
        {
            AccountId = Income,
            Credit = new Money(Currency.Of(CurrencyCode.Usd), 500m),
            AmountInCurrency = 500m,
            Currency = CurrencyCode.Usd,
        };

        var result = PostingBalance.Check([Debit(Cash, 500m), dollars]);

        Assert.Contains(result.Errors, error => error.Code == PostingErrorCode.MixedFunctionalCurrency);
    }

    [Fact]
    public void Balancing_in_taka_while_failing_in_dollars_is_caught()
    {
        // 4,200.00 BDT either way, so the functional totals tie. But the foreign amounts invoiced were
        // 20.00 and 19.90 USD: a 0.10 USD difference that would otherwise vanish without an entry.
        var invoice = new PostingLine
        {
            AccountId = Cash,
            Debit = Money.Bdt(4200m),
            AmountInCurrency = 20m,
            Currency = CurrencyCode.Usd,
            ExchangeRate = 210m,
        };

        var receipt = new PostingLine
        {
            AccountId = Income,
            Credit = Money.Bdt(4200m),
            AmountInCurrency = 19.90m,
            Currency = CurrencyCode.Usd,
            ExchangeRate = 210m,
        };

        var result = PostingBalance.Check([invoice, receipt]);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, error => error.Code == PostingErrorCode.UnbalancedInCurrency);
    }

    [Fact]
    public void A_zero_exchange_rate_is_refused()
    {
        var bad = new PostingLine
        {
            AccountId = Income,
            Credit = Money.Bdt(500m),
            AmountInCurrency = 500m,
            ExchangeRate = 0m,
        };

        var result = PostingBalance.Check([Debit(Cash, 500m), bad]);

        Assert.Contains(result.Errors, error => error.Code == PostingErrorCode.InvalidExchangeRate);
    }

    [Fact]
    public void A_rounding_residual_line_makes_an_otherwise_unbalanced_entry_balance()
    {
        var lines = new List<PostingLine> { Debit(Cash, 100m), Credit(Income, 99.97m) };

        var before = PostingBalance.Check(lines);
        Assert.False(before.Succeeded);

        var residual = PostingBalance.ResidualLine(
            Rounding,
            before.RoundingResidual,
            Currency.Of(CurrencyCode.Bdt),
            "Header rounding");

        Assert.NotNull(residual);
        lines.Add(residual!);

        var after = PostingBalance.Check(lines);
        Assert.True(after.Succeeded);
        Assert.Equal(100m, after.TotalDebit);
        Assert.Equal(100m, after.TotalCredit);
    }

    [Fact]
    public void A_functional_currency_line_that_never_sets_the_redundant_current_amount_still_balances()
    {
        // AmountInCurrency carries information only when the line names a different currency. Trusting it
        // for a taka line meant a caller that forgot the field contributed zero, and the entry came back
        // failing a currency check for a currency the posting never traded in.
        var result = PostingBalance.Check(
        [
            new PostingLine { AccountId = Cash, Debit = Money.Bdt(500m) },
            new PostingLine { AccountId = Income, Credit = Money.Bdt(500m) },
        ]);

        Assert.True(result.Succeeded);
        Assert.Equal(500m, result.CurrencyTotals[CurrencyCode.Bdt].Debit);
        Assert.Equal(500m, result.CurrencyTotals[CurrencyCode.Bdt].Credit);
    }

    [Fact]
    public void No_residual_line_is_offered_when_the_entry_already_balances() =>
        Assert.Null(PostingBalance.ResidualLine(Rounding, 0m, Currency.Of(CurrencyCode.Bdt), "Header rounding"));
}
