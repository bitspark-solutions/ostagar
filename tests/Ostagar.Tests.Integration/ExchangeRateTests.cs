using Ostagar.Core;
using Ostagar.Ledger;

namespace Ostagar.Tests.Integration;

/// <summary>
/// Proves the ledger decides which rate applies to a date, and refuses a foreign-currency line whose two
/// amounts do not follow from it.
/// </summary>
/// <remarks>
/// <para>
/// A stored rate that nothing reads would be the same defect as the audit interface it replaced: a table
/// of history with no consumer, so a backdated entry silently uses whatever number the caller felt like.
/// These tests post through <see cref="IPoster"/> and read the row back, which is the only way to check
/// which rate the ledger chose rather than which one was handed to it.
/// </para>
///
/// <para>
/// The amounts used throughout are real: 20.00 USD at 112.40 is exactly 2,248.00 taka, so the consistency
/// rule is exercised against arithmetic a person could check by hand rather than against a tolerance.
/// </para>
/// </remarks>
public sealed class ExchangeRateTests(WebAppFactory factory) : IClassFixture<WebAppFactory>
{
    private static readonly CurrencyCode Usd = CurrencyCode.Usd;
    private static readonly DateOnly July = new(2026, 7, 1);
    private static readonly DateOnly MidJuly = new(2026, 7, 15);
    private static readonly DateOnly August = new(2026, 8, 1);

    private readonly LedgerHarness ledger = new(factory);

    [Fact]
    public async Task A_foreign_line_without_a_rate_gets_the_one_effective_on_its_own_date()
    {
        var world = await ledger.ProvisionedCompanyAsync(2026);

        await ledger.RecordRateAsync(world, Usd, July, 112.40m);

        var outcome = await ledger.PostAsync(world, ForeignPair(world, 20m, 2_248m, rate: null));

        Assert.True(outcome.Succeeded, string.Join("; ", outcome.Errors.Select(each => each.Message)));

        var line = await ledger.PostedLineAsync(world, outcome.EntryId!.Value);

        Assert.Equal(112.40m, line.ExchangeRate);
        Assert.Equal(20m, line.AmountInCurrency);
        Assert.Equal(2_248m, line.FunctionalAmount);
    }

    /// <summary>
    /// The whole reason for effective dating, and the one a single current-rate column cannot express.
    /// </summary>
    [Fact]
    public async Task A_backdated_line_uses_the_rate_in_force_then_not_the_one_in_force_now()
    {
        var world = await ledger.ProvisionedCompanyAsync(2026);

        await ledger.RecordRateAsync(world, Usd, July, 110m);
        await ledger.RecordRateAsync(world, Usd, August, 118m);

        // Priced against July's row even though a newer one exists, and the newer one is what a
        // "current rate" design would have used.
        var july = await ledger.PostAsync(world, ForeignPair(world, 100m, 11_000m, rate: null, postingDate: MidJuly));
        var august = await ledger.PostAsync(world, ForeignPair(world, 100m, 11_800m, rate: null, postingDate: August));

        Assert.True(july.Succeeded, string.Join("; ", july.Errors.Select(each => each.Message)));
        Assert.Equal(110m, (await ledger.PostedLineAsync(world, july.EntryId!.Value)).ExchangeRate);
        Assert.Equal(118m, (await ledger.PostedLineAsync(world, august.EntryId!.Value)).ExchangeRate);
    }

    [Fact]
    public async Task A_rate_supplied_by_the_caller_is_checked_against_the_ledger_s_own_history()
    {
        var world = await ledger.ProvisionedCompanyAsync(2026);

        await ledger.RecordRateAsync(world, Usd, July, 112.40m);

        // The caller named its own rate, so the ledger honours it — but then the taka figure has to
        // follow from it, and 20.00 at 110 is 2,200.00 rather than the 2,300.00 stated here.
        var outcome = await ledger.PostAsync(world, ForeignPair(world, 20m, 2_300m, rate: 110m));

        Assert.Equal(PostingOutcomeStatus.Rejected, outcome.Status);
        Assert.Equal(PostingErrorCode.ExchangeRateAmountMismatch, outcome.Errors[0].Code);
        Assert.Contains("2200.00", outcome.Errors[0].Message, StringComparison.Ordinal);
        Assert.Contains("2300.00", outcome.Errors[0].Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_currency_the_company_never_priced_is_refused_naming_the_currency_and_the_date()
    {
        var world = await ledger.ProvisionedCompanyAsync(2026);

        var outcome = await ledger.PostAsync(world, ForeignPair(world, 20m, 2_248m, rate: null));

        Assert.Equal(PostingErrorCode.MissingExchangeRate, outcome.Errors[0].Code);
        Assert.Contains("USD", outcome.Errors[0].Message, StringComparison.Ordinal);
        Assert.Contains("2026-07-01", outcome.Errors[0].Message, StringComparison.Ordinal);
        Assert.Empty(await ledger.EntryIdsAsync(world));
    }

    [Fact]
    public async Task A_domestic_line_carries_no_rate_and_is_never_asked_for_one()
    {
        var world = await ledger.ProvisionedCompanyAsync(2026);

        var outcome = await ledger.PostAsync(world, amount: 500m);

        Assert.True(outcome.Succeeded);

        // Stored as par, because the ledger row always holds a definite number the schema can constrain;
        // inferred from the absence of a rate rather than from a default the caller might have forgotten.
        Assert.Equal(1m, (await ledger.PostedLineAsync(world, outcome.EntryId!.Value)).ExchangeRate);
    }

    [Fact]
    public async Task A_rate_for_the_company_s_own_currency_is_refused()
    {
        var world = await ledger.ProvisionedCompanyAsync(2026);

        var outcome = await ledger.TryRecordRateAsync(world, CurrencyCode.Bdt, July, 45m);

        // Not a harmless row. A line stated in taka is already in the ledger's own unit, so a rate beside
        // it would price a posting that needs no pricing, and 45 is a number someone could later believe.
        Assert.Equal(PostingOutcomeStatus.Rejected, outcome.Status);
        Assert.Contains("own currency", outcome.Errors[0].Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_second_rate_on_the_same_date_is_refused_rather_than_replacing_the_first()
    {
        var world = await ledger.ProvisionedCompanyAsync(2026);

        await ledger.RecordRateAsync(world, Usd, July, 112.40m);

        var second = await ledger.TryRecordRateAsync(world, Usd, July, 115m);

        // The correction is a row with a new effective date. Restating a rate the ledger may already have
        // posted against would change what an existing entry computes without touching the entry.
        Assert.Equal(PostingOutcomeStatus.Rejected, second.Status);
        Assert.Contains("append-only", second.Errors[0].Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Rate_history_cannot_be_rewritten()
    {
        var world = await ledger.ProvisionedCompanyAsync(2026);

        await ledger.RecordRateAsync(world, Usd, July, 112.40m);

        var failure = await ledger.TryMutateRateAsync(world, Usd, July);

        if (failure.SqlState == Npgsql.PostgresErrorCodes.InsufficientPrivilege)
            Assert.Contains("exchange_rates", failure.MessageText, StringComparison.Ordinal);
        var outcome = await ledger.PostAsync(world, ForeignPair(world, 20m, 2_248m, rate: null));
        Assert.True(outcome.Succeeded);
        Assert.Equal(112.40m, (await ledger.PostedLineAsync(world, outcome.EntryId!.Value)).ExchangeRate);
    }

    [Fact]
    public async Task One_company_cannot_price_its_lines_with_another_company_s_rate_history()
    {
        var priced = await ledger.ProvisionedCompanyAsync(2026);
        var unpriced = await ledger.ProvisionedCompanyAsync(2026);

        await ledger.RecordRateAsync(priced, Usd, July, 112.40m);

        var outcome = await ledger.PostAsync(unpriced, ForeignPair(unpriced, 20m, 2_248m, rate: null));

        Assert.Equal(PostingErrorCode.MissingExchangeRate, outcome.Errors[0].Code);
    }

    private static PostingRequest ForeignPair(
        LedgerWorld world,
        decimal foreignAmount,
        decimal functionalAmount,
        decimal? rate,
        DateOnly? postingDate = null)
    {
        var taka = Money.Bdt(functionalAmount);

        return new PostingRequest
        {
            JournalCode = "GL",
            PostingDate = postingDate ?? world.PostingDate,
            SourceType = "test.foreign",
            SourceId = Guid.NewGuid(),
            ActorUserId = LedgerHarness.Actor,
            Lines =
            [
                new PostingLine
                {
                    AccountId = world.Cash,
                    Debit = taka,
                    AmountInCurrency = foreignAmount,
                    Currency = Usd,
                    ExchangeRate = rate,
                },
                new PostingLine
                {
                    AccountId = world.Income,
                    Credit = taka,
                    AmountInCurrency = foreignAmount,
                    Currency = Usd,
                    ExchangeRate = rate,
                },
            ],
        };
    }
}
