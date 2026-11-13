using Ostagar.Ledger;
using Ostagar.Platform.Audit;
using Ostagar.Platform.Tenancy;
using Microsoft.Extensions.DependencyInjection;

namespace Ostagar.Tests.Integration;

/// <summary>
/// Proves the ledger keeps a record of who changed it — including of attempts that changed nothing.
/// </summary>
/// <remarks>
/// <see cref="IAuditWriter"/> existed in the platform layer with no implementation and no caller: an
/// interface that had never run. These tests are the first time anything was written to
/// <c>audit_logs</c> by the product, which is also the first time its two hardest properties could be
/// checked at all — that a posting and its record commit together, and that the record cannot be
/// rewritten afterwards.
/// </remarks>
public sealed class AuditTrailTests(WebAppFactory factory) : IClassFixture<WebAppFactory>
{
    private readonly LedgerHarness ledger = new(factory);

    [Fact]
    public async Task A_posting_appends_exactly_one_row_that_names_the_actor_and_the_entry()
    {
        var world = await ledger.ProvisionedCompanyAsync(2026);

        var outcome = await ledger.PostAsync(world, amount: 500m);

        var rows = await ledger.AuditRowsAsync(world);

        Assert.Single(rows);

        var row = rows[0];
        Assert.Equal("ledger.posting.post", row.Action);
        Assert.Equal("gl_entry", row.Resource);
        Assert.Equal(outcome.EntryId!.Value.ToString("D"), row.ResourceId);
        Assert.Equal(AuditOutcome.Allowed, row.Outcome);

        // The id is the point of the row. A display name is a convenience read from the request in
        // flight, and a posting made from a CLI or a job has none to read; the declared actor id is what
        // the trail is actually relied upon, and it is required rather than inferred.
        Assert.Equal(LedgerHarness.Actor, row.ActorUserId);
        Assert.Contains("GL-000001", row.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_refused_posting_is_recorded_and_no_entry_exists()
    {
        var world = await ledger.ProvisionedCompanyAsync(2026);

        var outcome = await ledger.PostAsync(world, new PostingRequest
        {
            ActorUserId = LedgerHarness.Actor,
            JournalCode = "GL",
            PostingDate = world.PostingDate,
            SourceType = "test.manual",
            Lines =
            [
                LedgerHarness.Debit(world.Cash, 500m),
                LedgerHarness.Credit(world.Income, 400m),
            ],
        });

        Assert.Equal(PostingOutcomeStatus.Rejected, outcome.Status);

        var rows = await ledger.AuditRowsAsync(world);

        Assert.Single(rows);
        Assert.Equal("ledger.posting.reject", rows[0].Action);
        Assert.Equal(AuditOutcome.Failed, rows[0].Outcome);

        // Nothing was written, so the row names no resource. An id here would be a fabricated one, which
        // is the same defect law L3 bans on the success path arriving through the logging side.
        Assert.Equal(string.Empty, rows[0].ResourceId);
        Assert.Empty(await ledger.EntryIdsAsync(world));
    }

    [Fact]
    public async Task A_retry_leaves_a_record_and_not_a_second_entry()
    {
        var world = await ledger.ProvisionedCompanyAsync(2026);
        var sourceId = Guid.NewGuid();

        await ledger.PostAsync(world, sourceType: "sales.invoice", sourceId: sourceId);
        await ledger.PostAsync(world, sourceType: "sales.invoice", sourceId: sourceId);

        var rows = await ledger.AuditRowsAsync(world);

        Assert.Equal(2, rows.Count);
        Assert.Equal(["ledger.posting.post", "ledger.posting.retry"], rows.Select(each => each.Action));
        Assert.Single(await ledger.EntryIdsAsync(world));

        // The two rows share a resource id: the retry points at the entry the first attempt created,
        // which is what makes a duplicated payment traceable rather than merely visible.
        Assert.Equal(rows[0].ResourceId, rows[1].ResourceId);
    }

    [Fact]
    public async Task The_trail_is_scoped_to_its_company()
    {
        var mine = await ledger.ProvisionedCompanyAsync(2026);
        var other = await ledger.ProvisionedCompanyAsync(2026);

        await ledger.PostAsync(mine, amount: 500m);

        Assert.Single(await ledger.AuditRowsAsync(mine));
        Assert.Empty(await ledger.AuditRowsAsync(other));
    }

    [Fact]
    public async Task The_audit_trail_refuses_to_be_rewritten()
    {
        var world = await ledger.ProvisionedCompanyAsync(2026);

        await ledger.PostAsync(world, amount: 500m);
        var before = Assert.Single(await ledger.AuditRowsAsync(world));

        var failure = await ledger.TryMutateAuditRowAsync(world, delete: false);

        // The migration's own comment records why a privilege check is not enough here: this connection
        // is the table owner, which outranks a REVOKE. The trigger is the line that binds it.
        if (failure.SqlState == Npgsql.PostgresErrorCodes.InsufficientPrivilege)
            Assert.Contains("audit_logs", failure.MessageText, StringComparison.Ordinal);
        Assert.Equal(before, Assert.Single(await ledger.AuditRowsAsync(world)));
    }

    [Fact]
    public async Task The_audit_trail_refuses_to_be_deleted()
    {
        var world = await ledger.ProvisionedCompanyAsync(2026);

        await ledger.PostAsync(world, amount: 500m);
        var before = Assert.Single(await ledger.AuditRowsAsync(world));

        var failure = await ledger.TryMutateAuditRowAsync(world, delete: true);

        if (failure.SqlState == Npgsql.PostgresErrorCodes.InsufficientPrivilege)
            Assert.Contains("audit_logs", failure.MessageText, StringComparison.Ordinal);
        Assert.Equal(before, Assert.Single(await ledger.AuditRowsAsync(world)));
    }

    [Fact]
    public async Task Rows_can_still_be_appended_after_the_trail_is_locked_down()
    {
        var world = await ledger.ProvisionedCompanyAsync(2026);

        await ledger.PostAsync(world, amount: 100m);
        await ledger.PostAsync(world, amount: 200m);
        await ledger.PostAsync(world, amount: 300m);

        // The negative control the other two tests need: a trigger that refused INSERT as well would make
        // "cannot be rewritten" trivially true while leaving the ledger with no trail at all.
        Assert.Equal(3, (await ledger.AuditRowsAsync(world)).Count);
    }

    [Fact]
    public async Task An_audit_entry_cannot_be_appended_without_a_resolved_tenant()
    {
        using var systemScope = TenantScope.OpenSystem(
            factory.Services.GetRequiredService<IServiceScopeFactory>());

        var writer = systemScope.ServiceProvider.GetRequiredService<IAuditWriter>();

        var failure = await Record.ExceptionAsync(() => Task.Run(() => writer.Append(new AuditRecord(
            ActorUserId: null,
            ActorName: "nobody",
            Action: "ledger.test",
            Resource: "gl_entry",
            ResourceId: "none",
            Outcome: AuditOutcome.Allowed))));

        // A row with no tenant belongs to no company: it can never be produced for them in a disclosure
        // and can never be defended as theirs. Refusing it is the only honest option, and it is why this
        // is an exception rather than a dropped write.
        Assert.IsType<InvalidOperationException>(failure);
        Assert.Contains("without a resolved tenant", failure!.Message, StringComparison.Ordinal);
    }
}
