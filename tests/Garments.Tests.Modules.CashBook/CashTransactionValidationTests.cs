using Garments.Core;
using Garments.Modules.CashBook;

namespace Garments.Tests.Modules.CashBook;

public sealed class CashTransactionValidationTests
{
    private static readonly Guid CashAccount = Guid.NewGuid();
    private static readonly Guid CounterAccount = Guid.NewGuid();

    [Fact]
    public void A_valid_receipt_passes()
    {
        var decision = CashTransactionValidator.Validate(Draft());

        Assert.True(decision.Succeeded);
    }

    [Fact]
    public void A_zero_amount_is_refused_not_silenced()
    {
        var decision = CashTransactionValidator.Validate(Draft() with
        {
            Amount = Money.Bdt(0m),
        });

        Assert.Contains(decision.Errors, each => each.Code == CashTransactionErrorCode.AmountNotPositive);
    }

    [Fact]
    public void A_negative_amount_is_refused_because_the_kind_already_states_direction()
    {
        var decision = CashTransactionValidator.Validate(Draft() with
        {
            Amount = Money.Bdt(-500m),
        });

        Assert.Contains(decision.Errors, each => each.Code == CashTransactionErrorCode.AmountNotPositive);
    }

    [Fact]
    public void A_missing_amount_is_refused()
    {
        var decision = CashTransactionValidator.Validate(Draft() with { Amount = null });

        Assert.Contains(decision.Errors, each => each.Code == CashTransactionErrorCode.AmountMissing);
    }

    [Fact]
    public void The_same_account_on_both_sides_is_refused()
    {
        var decision = CashTransactionValidator.Validate(Draft() with
        {
            CashAccountId = CashAccount,
            CounterAccountId = CashAccount,
        });

        Assert.Contains(decision.Errors, each => each.Code == CashTransactionErrorCode.SameAccountBothSides);
    }

    [Fact]
    public void An_empty_account_is_refused()
    {
        var decision = CashTransactionValidator.Validate(Draft() with { CounterAccountId = Guid.Empty });

        Assert.Contains(decision.Errors, each => each.Code == CashTransactionErrorCode.AccountMissing);
    }

    [Fact]
    public void Several_mistakes_come_back_together()
    {
        var decision = CashTransactionValidator.Validate(Draft() with
        {
            Amount = null,
            CashAccountId = Guid.Empty,
        });

        Assert.True(decision.Errors.Count >= 2);
    }

    private static NewCashTransaction Draft() => new()
    {
        Kind = CashTransactionKind.Receipt,
        TransactionDate = new DateOnly(2026, 11, 1),
        CashAccountId = CashAccount,
        CounterAccountId = CounterAccount,
        Amount = Money.Bdt(1_500m),
    };
}
