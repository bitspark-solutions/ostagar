using Ostagar.Ledger;

namespace Ostagar.Tests.Unit;

public class NormalBalanceOfTests
{
    [Theory]
    [InlineData(AccountType.Asset, NormalBalance.Debit)]
    [InlineData(AccountType.Expense, NormalBalance.Debit)]
    [InlineData(AccountType.Liability, NormalBalance.Credit)]
    [InlineData(AccountType.Equity, NormalBalance.Credit)]
    [InlineData(AccountType.Income, NormalBalance.Credit)]
    public void Normal_balance_follows_the_account_type(AccountType type, NormalBalance expected) =>
        Assert.Equal(expected, NormalBalanceOf.For(type));

    [Fact]
    public void An_asset_reads_positive_when_debits_exceed_credits()
    {
        var balance = NormalBalanceOf.Balance(100m, 40m, AccountType.Asset);

        Assert.Equal(60m, balance);
    }

    [Fact]
    public void A_liability_reads_positive_when_credits_exceed_debits()
    {
        var balance = NormalBalanceOf.Balance(40m, 100m, AccountType.Liability);

        Assert.Equal(60m, balance);
    }

    [Fact]
    public void A_bank_overdraft_is_negative_rather_than_disguised()
    {
        // The legacy grid wrapped every figure in Math.abs, so this rendered as a positive balance and
        // a reader could not tell an overdraft from cash at all.
        var balance = NormalBalanceOf.Balance(10m, 50m, AccountType.Asset);

        Assert.Equal(-40m, balance);
        Assert.True(balance < 0m);
    }

    [Fact]
    public void The_two_sides_of_a_balanced_ledger_offset_exactly()
    {
        var asset = NormalBalanceOf.Balance(500m, 0m, AccountType.Asset);
        var income = NormalBalanceOf.Balance(0m, 500m, AccountType.Income);

        Assert.Equal(0m, asset - income);
    }
}
