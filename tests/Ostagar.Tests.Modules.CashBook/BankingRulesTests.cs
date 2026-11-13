using Ostagar.Core;
using Ostagar.Ledger;
using Ostagar.Modules.CashBook;

namespace Ostagar.Tests.Modules.CashBook;

public sealed class BankingRulesTests
{
    private static readonly DateOnly Date = new(2026, 7, 31);

    [Fact]
    public void Transfer_uses_BANK_and_preserves_the_retry_key_and_both_currency_amounts()
    {
        var from = Account();
        var to = Account();
        var id = Guid.NewGuid();
        var request = BankTransferPosting.Build(id, from, to, Date, new Money(Currency.Of(CurrencyCode.Bdt), 125m),
            new Money(Currency.Of(CurrencyCode.Bdt), 125m), Guid.NewGuid(), string.Empty);
        Assert.Equal(StandardJournals.BankCode, request.JournalCode);
        Assert.Equal(id, request.SourceId);
        Assert.Equal(BankingVocabulary.TransferSource, request.SourceType);
        Assert.Equal(from.LedgerAccountId, request.Lines.Single(x => x.Credit != null).AccountId);
        Assert.Equal(to.LedgerAccountId, request.Lines.Single(x => x.Debit != null).AccountId);
        Assert.Empty(PostingBalance.Check(request.Lines).Errors);
    }

    [Fact]
    public void Transfer_to_itself_is_refused()
    {
        var account = Account();
        var error = Assert.Throws<BankingRuleException>(() => BankTransferPosting.Build(Guid.NewGuid(),
            account, account, Date, new Money(Currency.Of(CurrencyCode.Bdt), 1m), new Money(Currency.Of(CurrencyCode.Bdt), 1m),
            Guid.NewGuid(), string.Empty));
        Assert.Equal(BankingErrorCode.SameAccount, error.Code);
    }

    [Fact]
    public void Reconciliation_requires_exact_tie_without_a_tolerance()
    {
        var error = Assert.Throws<BankingRuleException>(() => BankReconciliationRules.Validate(
            Date, Date, 100m, 110m, [10.01m], 1, 1));
        Assert.Equal(BankingErrorCode.StatementUnbalanced, error.Code);
        BankReconciliationRules.Validate(Date, Date, 100m, 110m, [10m], 1, 1);
    }

    [Fact]
    public void Missing_or_duplicate_lines_are_not_silently_dropped()
    {
        Assert.Throws<BankingRuleException>(() => BankReconciliationRules.Validate(
            Date, Date, 0m, 0m, [], 2, 1));
        Assert.Throws<BankingRuleException>(() => BankReconciliationRules.Validate(
            Date, Date, 0m, 0m, [], 1, 1));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Transfer_requires_a_positive_amount(decimal amount)
    {
        var error = Assert.Throws<BankingRuleException>(() => BankTransferPosting.Build(Guid.NewGuid(),
            Account(), Account(), Date, Money.Bdt(amount), Money.Bdt(amount), Guid.NewGuid(), string.Empty));
        Assert.Equal(BankingErrorCode.AmountNotPositive, error.Code);
    }

    [Fact]
    public void Foreign_currency_amount_and_functional_amount_remain_distinct()
    {
        var from = Account();
        var to = Account();
        from.Currency = to.Currency = CurrencyCode.From("USD");
        var request = BankTransferPosting.Build(Guid.NewGuid(), from, to, Date,
            new Money(Currency.Of(from.Currency), 10m), Money.Bdt(1200m), Guid.NewGuid(), string.Empty);
        Assert.All(request.Lines, x => Assert.Equal(10m, x.AmountInCurrency));
        Assert.Equal(1200m, request.Lines.Single(x => x.Debit != null).Debit!.Value.Amount);
        to.Currency = CurrencyCode.Bdt;
        Assert.Throws<BankingRuleException>(() => BankTransferPosting.Build(Guid.NewGuid(), from, to, Date,
            new Money(Currency.Of(from.Currency), 10m), Money.Bdt(1200m), Guid.NewGuid(), string.Empty));
    }

    [Fact]
    public void Statement_sequence_preserves_dates_and_opening_balance()
    {
        var previous = new BankReconciliation { To = Date, ClosingBalance = 100m };
        BankReconciliationRules.ValidateSequence(previous, Date.AddDays(1), 100m);
        Assert.Throws<BankingRuleException>(() => BankReconciliationRules.ValidateSequence(previous, Date, 100m));
        Assert.Throws<BankingRuleException>(() => BankReconciliationRules.ValidateSequence(previous, Date.AddDays(1), 99m));
        Assert.Throws<BankingRuleException>(() => BankReconciliationRules.ValidateSequence(previous, Date.AddDays(2), 100m));
    }

    [Fact]
    public void Outstanding_items_adjust_the_bank_statement_without_hiding_a_difference()
    {
        var report = new BankStatementReport("BDT", Date, 100m,
        [
            new BankStatementLine(Guid.NewGuid(), Guid.NewGuid(), "BNK-1", Date, string.Empty, 100m, true),
            new BankStatementLine(Guid.NewGuid(), Guid.NewGuid(), "BNK-2", Date, string.Empty, -20m, false),
        ]);
        Assert.Equal(80m, report.LedgerBalance);
        Assert.Equal(-20m, report.OutstandingMovement);
        Assert.Equal(0m, report.Difference);
        Assert.Equal(-1m, (report with { StatementClosingBalance = 101m }).Difference);
    }

    private static BankAccount Account() => new()
    {
        Id = Guid.NewGuid(), LedgerAccountId = Guid.NewGuid(), Code = "BANK-TEST",
        BankName = "Test bank", AccountNumber = "123", Currency = CurrencyCode.Bdt,
    };
}
