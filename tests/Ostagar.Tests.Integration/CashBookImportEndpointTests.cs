using System.Net;
using System.Net.Http.Json;
using Ostagar.Platform.Audit;
using Ostagar.Platform.Rbac;

namespace Ostagar.Tests.Integration;

/// <summary>
/// Proves the P3 gate over real HTTP: the client's two-sided CSV lands as balanced ledger postings
/// through <c>IPoster</c>, each row reports its own outcome, a re-upload of the same bytes posts
/// nothing twice, and the attempt is written to the audit trail either way.
/// </summary>
public sealed class CashBookImportEndpointTests(WebAppFactory factory) : IClassFixture<WebAppFactory>
{
    private readonly LedgerHarness ledger = new(factory);

    private const string Header =
        "Date,Catagories,Particulars,Amount,Date,Catagories,Supplier,Buyer,Particulars,Amount";

    [Fact]
    public async Task A_client_csv_posts_balanced_entries_through_the_ledger()
    {
        var company = await ledger.ProvisionedCompanyWithUserAsync(DefaultRoleMatrix.Accounts);
        var world = company.World;

        var response = await company.Client.SendAsync(LedgerHarness.Authenticated(
            company.Token,
            HttpMethod.Post,
            "/api/v1/cash-book/import",
            ImportBody(
                world,
                Header + "\n"
                + "15-07-2026,Received: Urbo ltd,inv 9,\"50,000\",15-07-26,Salary A/C,,,july wages,\"12,500\"")));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var summary = await response.Content.ReadFromJsonAsync<ImportSummary>();
        Assert.NotNull(summary);
        Assert.Equal(2, summary.Posted);
        Assert.Equal(0, summary.Refused);
        Assert.Equal(2, summary.Rows.Count);
        Assert.All(summary.Rows, row => Assert.Equal("posted", row.Outcome));
        Assert.Equal("CASH-000001", summary.Rows[0].Number);
        Assert.Equal("CASH-000002", summary.Rows[1].Number);

        // The money is real in the ledger: the receipt debited cash and the payment credited it.
        var net = await ledger.NetByAccountAsync(world);
        Assert.Equal(50_000m - 12_500m, net[world.Cash]);
        Assert.Equal(-50_000m + 12_500m, net[world.Income]);

        // And the whole attempt is on the record.
        var auditRows = await ledger.AuditRowsAsync(world);
        Assert.Contains(auditRows, row => row.Action == "cash.import" && row.Outcome == AuditOutcome.Allowed);
    }

    [Fact]
    public async Task Reuploading_the_same_file_posts_nothing_twice()
    {
        var company = await ledger.ProvisionedCompanyWithUserAsync(DefaultRoleMatrix.Accounts);
        var world = company.World;
        var content = Header + "\n" + "15-07-2026,Received: Urbo ltd,inv 9,\"50,000\",,,,,,";

        var first = await company.Client.SendAsync(LedgerHarness.Authenticated(
            company.Token, HttpMethod.Post, "/api/v1/cash-book/import", ImportBody(world, content)));
        var firstSummary = await first.Content.ReadFromJsonAsync<ImportSummary>();
        Assert.NotNull(firstSummary);
        Assert.Equal(1, firstSummary.Posted);

        var second = await company.Client.SendAsync(LedgerHarness.Authenticated(
            company.Token, HttpMethod.Post, "/api/v1/cash-book/import", ImportBody(world, content)));
        var secondSummary = await second.Content.ReadFromJsonAsync<ImportSummary>();
        Assert.NotNull(secondSummary);
        Assert.Equal(0, secondSummary.Posted);
        Assert.Equal(1, secondSummary.AlreadyPosted);

        // One entry, one document, unchanged balances: the retry resolved rather than repeating.
        Assert.Single(await ledger.EntryIdsAsync(world));
        var net = await ledger.NetByAccountAsync(world);
        Assert.Equal(50_000m, net[world.Cash]);
    }

    [Fact]
    public async Task A_row_with_no_account_mapping_is_refused_while_the_rest_of_the_file_posts()
    {
        var company = await ledger.ProvisionedCompanyWithUserAsync(DefaultRoleMatrix.Accounts);
        var world = company.World;

        var response = await company.Client.SendAsync(LedgerHarness.Authenticated(
            company.Token,
            HttpMethod.Post,
            "/api/v1/cash-book/import",
            ImportBody(
                world,
                Header + "\n"
                + "15-07-2026,Received: Urbo ltd,inv 9,\"50,000\",,,,,," + "\n"
                + "16-07-2026,Mystery Category,inv 10,\"7,000\",,,,,,")));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var summary = await response.Content.ReadFromJsonAsync<ImportSummary>();
        Assert.NotNull(summary);
        Assert.Equal(1, summary.Posted);
        Assert.Equal(1, summary.Refused);

        var refused = summary.Rows.Single(row => row.Outcome == "refused");
        Assert.Equal(3, refused.Line);
        Assert.Contains(refused.Errors, each => each.Code == "CategoryUnmapped");
    }

    [Fact]
    public async Task A_file_with_no_header_is_refused_and_posts_nothing()
    {
        var company = await ledger.ProvisionedCompanyWithUserAsync(DefaultRoleMatrix.Accounts);
        var world = company.World;

        var response = await company.Client.SendAsync(LedgerHarness.Authenticated(
            company.Token, HttpMethod.Post, "/api/v1/cash-book/import",
            ImportBody(world, "not,a,cash,book\n1,2,3,4,5,6,7,8,9,10")));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var summary = await response.Content.ReadFromJsonAsync<ImportSummary>();
        Assert.NotNull(summary);
        Assert.Equal(0, summary.Posted);
        Assert.Contains(summary.Rows, row =>
            row.Outcome == "refused" && row.Errors.Any(each => each.Code == "FileHeaderMissing"));

        Assert.Empty(await ledger.EntryIdsAsync(world));
    }

    [Fact]
    public async Task A_clerk_without_the_accounts_role_cannot_import()
    {
        var company = await ledger.ProvisionedCompanyWithUserAsync(DefaultRoleMatrix.Stock);

        var response = await company.Client.SendAsync(LedgerHarness.Authenticated(
            company.Token, HttpMethod.Post, "/api/v1/cash-book/import",
            ImportBody(company.World, Header + "\n15-07-2026,Received: Urbo ltd,inv 9,\"50,000\",,,,,,")));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>
    /// The body every test sends: the file's text, the world's cash account, and the explicit
    /// category → account-code mapping. Imported categories map to the fixture's income account
    /// ("4010") — any postable counter account proves the plumbing; the mapping being explicit is the
    /// point. "Mystery Category" deliberately names a code the company does not have.
    /// </summary>
    private static object ImportBody(LedgerWorld world, string content) => new
    {
        Content = content,
        FileName = "client-cash-book.csv",
        CashAccountId = world.Cash,
        CategoryAccountCodes = new Dictionary<string, string>
        {
            ["Received: Urbo ltd"] = "4010",
            ["Salary A/C"] = "4010",
            ["Mystery Category"] = "9999",
        },
    };

    private sealed record ImportSummary(
        string FileFingerprint,
        int Posted,
        int AlreadyPosted,
        int Refused,
        List<ImportRow> Rows);

    private sealed record ImportRow(
        int Line,
        string? Kind,
        string Outcome,
        Guid? TransactionId,
        Guid? EntryId,
        string? Number,
        List<ImportRowError> Errors);

    private sealed record ImportRowError(string Code, string Message);
}
