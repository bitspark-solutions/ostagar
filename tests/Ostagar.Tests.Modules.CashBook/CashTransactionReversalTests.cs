using Ostagar.Core;
using Ostagar.Modules.CashBook;

namespace Ostagar.Tests.Modules.CashBook;

public sealed class CashTransactionReversalTests
{
    [Fact]
    public void A_posted_transaction_may_be_reversed()
    {
        var decision = CashTransactionValidator.CanReverse(Transaction(posted: true));

        Assert.True(decision.Succeeded);
    }

    [Fact]
    public void An_unposted_transaction_has_nothing_to_reverse()
    {
        var decision = CashTransactionValidator.CanReverse(Transaction(posted: false));

        Assert.False(decision.Succeeded);
        Assert.Contains(decision.Errors, each => each.Code == CashTransactionErrorCode.TransactionNotPosted);
    }

    [Fact]
    public void An_already_reversed_transaction_refuses_a_second_reversal()
    {
        var transaction = Transaction(posted: true);
        transaction.Status = CashTransactionStatus.Reversed;
        transaction.ReversedByEntryId = Guid.NewGuid();

        var decision = CashTransactionValidator.CanReverse(transaction);

        Assert.False(decision.Succeeded);
        Assert.Contains(decision.Errors, each => each.Code == CashTransactionErrorCode.TransactionAlreadyReversed);
    }

    private static CashTransaction Transaction(bool posted) => new()
    {
        Id = Guid.NewGuid(),
        Kind = CashTransactionKind.Receipt,
        TransactionDate = new DateOnly(2026, 11, 1),
        CashAccountId = Guid.NewGuid(),
        CounterAccountId = Guid.NewGuid(),
        Amount = 900m,
        Currency = CurrencyCode.Bdt,
        ActorUserId = Guid.NewGuid(),
        PostedEntryId = posted ? Guid.NewGuid() : null,
    };
}
