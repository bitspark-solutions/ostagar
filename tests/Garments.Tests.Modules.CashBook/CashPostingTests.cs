using Garments.Core;
using Garments.Ledger;
using Garments.Modules.CashBook;

namespace Garments.Tests.Modules.CashBook;

public sealed class CashPostingTests
{
    private static readonly Guid CashAccount = Guid.NewGuid();
    private static readonly Guid CounterAccount = Guid.NewGuid();
    private static readonly Guid Actor = Guid.NewGuid();

    [Fact]
    public void A_receipt_debits_cash_and_credits_the_counter_account()
    {
        var plan = CashPosting.For(Transaction(CashTransactionKind.Receipt), AccountSubType.Cash);

        Assert.True(plan.Succeeded);
        var lines = plan.Request!.Lines;
        Assert.Equal(2, lines.Count);
        Assert.Equal(CashAccount, lines[0].AccountId);
        Assert.Equal(1_500m, lines[0].Debit!.Value.Amount);
        Assert.Null(lines[0].Credit);
        Assert.Equal(CounterAccount, lines[1].AccountId);
        Assert.Equal(1_500m, lines[1].Credit!.Value.Amount);
        Assert.Null(lines[1].Debit);
    }

    [Fact]
    public void A_payment_debits_the_counter_account_and_credits_cash()
    {
        var plan = CashPosting.For(Transaction(CashTransactionKind.Payment), AccountSubType.Cash);

        var lines = plan.Request!.Lines;
        Assert.Equal(1_500m, lines[0].Credit!.Value.Amount);
        Assert.Equal(1_500m, lines[1].Debit!.Value.Amount);
    }

    [Fact]
    public void A_cash_account_posts_to_the_cash_journal()
    {
        var plan = CashPosting.For(Transaction(CashTransactionKind.Receipt), AccountSubType.Cash);

        Assert.Equal(StandardJournals.CashCode, plan.Request!.JournalCode);
    }

    [Fact]
    public void A_bank_account_posts_to_the_bank_journal()
    {
        var plan = CashPosting.For(Transaction(CashTransactionKind.Receipt), AccountSubType.Bank);

        Assert.Equal(StandardJournals.BankCode, plan.Request!.JournalCode);
    }

    [Fact]
    public void A_non_cash_account_on_the_cash_side_is_refused()
    {
        var plan = CashPosting.For(Transaction(CashTransactionKind.Receipt), AccountSubType.SalesRevenue);

        Assert.False(plan.Succeeded);
        Assert.Contains(plan.Errors, each => each.Code == CashTransactionErrorCode.CashAccountNotCashLike);
    }

    [Fact]
    public void The_source_key_makes_a_retry_idempotent()
    {
        var transaction = Transaction(CashTransactionKind.Receipt);

        var plan = CashPosting.For(transaction, AccountSubType.Cash);

        Assert.Equal(CashSourceTypes.Receipt, plan.Request!.SourceType);
        Assert.Equal(transaction.Id, plan.Request.SourceId);
        Assert.Equal(transaction.Iteration, plan.Request.Iteration);
        Assert.Equal(transaction.ActorUserId, plan.Request.ActorUserId);
    }

    private static CashTransaction Transaction(CashTransactionKind kind) => new()
    {
        Id = Guid.NewGuid(),
        Kind = kind,
        TransactionDate = new DateOnly(2026, 11, 1),
        CashAccountId = CashAccount,
        CounterAccountId = CounterAccount,
        Amount = 1_500m,
        Currency = CurrencyCode.Bdt,
        ActorUserId = Actor,
        Memo = "sale to walk-in",
    };
}
