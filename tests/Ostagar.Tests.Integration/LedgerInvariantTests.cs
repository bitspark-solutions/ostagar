using Ostagar.Core;
using Ostagar.Data;
using Ostagar.Ledger;
using Ostagar.Platform.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Ostagar.Tests.Integration;

/// <summary>
/// Proves the ledger's accounting rules reject a wrong posting <i>in the database</i>.
/// </summary>
/// <remarks>
/// <para>
/// This is the P2 gate. The invariants are only real if a write that breaks one fails, so each test
/// attempts a genuinely invalid posting against the migrated schema and asserts the database refused it,
/// naming which invariant fired.
/// </para>
///
/// <para>
/// Written against <see cref="OstagarDbContext"/> on a real Postgres rather than through
/// <see cref="IPoster"/>, because what is under test is the schema. A posting path that validates in C#
/// over a schema that enforces nothing would pass an end-to-end test and still corrupt a ledger on the
/// first code path that forgot to validate — and the legacy build is that case study.
/// </para>
///
/// <para>
/// <b>Each refusal has a matching acceptance.</b> The positive control below is not a courtesy: five
/// triggers that rejected everything would make the other tests green while the product was unusable.
/// </para>
/// </remarks>
public sealed class LedgerInvariantTests(WebAppFactory factory) : IClassFixture<WebAppFactory>
{
    private readonly LedgerHarness ledger = new(factory);

    [Fact]
    public async Task A_balanced_entry_in_an_open_period_posts()
    {
        var world = await ledger.ProvisionAsync();

        var failure = await Record.ExceptionAsync(() => ledger.WriteRawAsync(world, entry =>
        {
            entry.Lines.Add(LedgerHarness.Line(world.Cash, debit: 500m));
            entry.Lines.Add(LedgerHarness.Line(world.Income, credit: 500m));
        }));

        Assert.Null(failure);
    }

    [Fact]
    public async Task An_unbalanced_posted_entry_is_refused_by_the_database()
    {
        var world = await ledger.ProvisionAsync();

        var message = await LedgerHarness.StoreErrorAsync(() => ledger.WriteRawAsync(world, entry =>
        {
            entry.Lines.Add(LedgerHarness.Line(world.Cash, debit: 500m));
            entry.Lines.Add(LedgerHarness.Line(world.Income, credit: 499.99m));
        }));

        Assert.Contains("Invariant I1", message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_one_paisa_imbalance_is_refused_because_there_is_no_tolerance()
    {
        var world = await ledger.ProvisionAsync();

        var message = await LedgerHarness.StoreErrorAsync(() => ledger.WriteRawAsync(world, entry =>
        {
            entry.Lines.Add(LedgerHarness.Line(world.Cash, debit: 1.00m));
            entry.Lines.Add(LedgerHarness.Line(world.Income, credit: 0.99m));
        }));

        Assert.Contains("Invariant I1", message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_single_sided_posted_entry_is_refused()
    {
        // The exact row the legacy cash book wrote: one line, posted, nothing checking it. The line-level
        // CHECK passes, because one line carrying a debit and no credit is a well-formed line.
        var world = await ledger.ProvisionAsync();

        var message = await LedgerHarness.StoreErrorAsync(() => ledger.WriteRawAsync(world, entry =>
        {
            entry.Lines.Add(LedgerHarness.Line(world.Cash, debit: 500m));
        }));

        Assert.Contains("Invariant I1", message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_line_with_both_sides_is_refused()
    {
        var world = await ledger.ProvisionAsync();

        var message = await LedgerHarness.StoreErrorAsync(() => ledger.WriteRawAsync(world, entry =>
        {
            entry.Lines.Add(new GlLine
            {
                TenantId = world.TenantId,
                AccountId = world.Cash,
                LineOrder = 1,
                Debit = 100m,
                Credit = 100m,
                Currency = CurrencyCode.Bdt,
                AmountInCurrency = 100m,
                ExchangeRate = 1m,
            });
        }));

        Assert.Contains("CK_gl_line_debit_xor_credit", message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_posted_entry_cannot_be_changed()
    {
        var world = await PostBalancedAsync();

        var message = await LedgerHarness.StoreErrorAsync(async () =>
        {
            using var scope = TenantScope.Open(
                factory.Services.GetRequiredService<IServiceScopeFactory>(), world.TenantId);

            var context = scope.ServiceProvider.GetRequiredService<OstagarDbContext>();

            // The memo is deliberately the only thing touched: correcting a posting by editing its text
            // is how an audit trail becomes decoration while every number still looks reconciled.
            var posted = await context.GlEntries.SingleAsync();
            posted.Memo = "looks fine now";

            await context.SaveChangesAsync();
        });

        Assert.Contains("Invariant I3", message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_line_cannot_be_added_to_a_posted_entry()
    {
        var world = await PostBalancedAsync();

        // A balanced pair appended on its own satisfies every other rule in the enforcement migration;
        // only immutability refuses it. This is why the line trigger covers insert, not just update.
        var message = await LedgerHarness.StoreErrorAsync(async () =>
        {
            using var scope = TenantScope.Open(
                factory.Services.GetRequiredService<IServiceScopeFactory>(), world.TenantId);

            var context = scope.ServiceProvider.GetRequiredService<OstagarDbContext>();

            var posted = await context.GlEntries.SingleAsync();

            context.GlLines.Add(new GlLine
            {
                TenantId = world.TenantId,
                EntryId = posted.Id,
                AccountId = world.Income,
                LineOrder = 3,
                Credit = 250m,
                Currency = CurrencyCode.Bdt,
                AmountInCurrency = 250m,
                ExchangeRate = 1m,
            });

            context.GlLines.Add(new GlLine
            {
                TenantId = world.TenantId,
                EntryId = posted.Id,
                AccountId = world.Cash,
                LineOrder = 4,
                Debit = 250m,
                Currency = CurrencyCode.Bdt,
                AmountInCurrency = 250m,
                ExchangeRate = 1m,
            });

            await context.SaveChangesAsync();
        });

        Assert.Contains("Invariant I3", message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_posted_entrys_lines_cannot_be_deleted()
    {
        var world = await PostBalancedAsync();

        var message = await LedgerHarness.StoreErrorAsync(async () =>
        {
            using var scope = TenantScope.Open(
                factory.Services.GetRequiredService<IServiceScopeFactory>(), world.TenantId);

            var context = scope.ServiceProvider.GetRequiredService<OstagarDbContext>();

            var line = await context.GlLines.OrderBy(each => each.LineOrder).Skip(1).SingleAsync();
            context.GlLines.Remove(line);

            await context.SaveChangesAsync();
        });

        Assert.Contains("Invariant I3", message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_locked_period_refuses_a_backdated_posting()
    {
        var world = await ledger.ProvisionAsync();

        await ledger.LockPeriodAsync(world);

        var message = await LedgerHarness.StoreErrorAsync(() => ledger.WriteRawAsync(world, entry =>
        {
            entry.Lines.Add(LedgerHarness.Line(world.Cash, debit: 500m));
            entry.Lines.Add(LedgerHarness.Line(world.Income, credit: 500m));
        }));

        Assert.Contains("Invariant I6", message, StringComparison.Ordinal);
    }

    private async Task<LedgerWorld> PostBalancedAsync()
    {
        var world = await ledger.ProvisionAsync();

        await ledger.WriteRawAsync(world, entry =>
        {
            entry.Lines.Add(LedgerHarness.Line(world.Cash, debit: 500m));
            entry.Lines.Add(LedgerHarness.Line(world.Income, credit: 500m));
        });

        return world;
    }

}
