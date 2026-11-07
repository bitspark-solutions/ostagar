using Ostagar.Core;
using Ostagar.Data;
using Ostagar.Ledger;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Ostagar.Platform.Audit;
using Ostagar.Platform.Identity;
using Ostagar.Platform.Rbac;
using Ostagar.Platform.Tenancy;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Ostagar.Tests.Integration;

/// <summary>
/// Creates one company's worth of ledger and writes postings into it, for the suites that test the
/// kernel rather than the endpoints.
/// </summary>
/// <remarks>
/// Extracted once a second suite needed the same world. Two copies of ledger provisioning is how a test
/// suite ends up proving different things while looking like it proves the same ones: the day a rule
/// changes in one copy, the other keeps passing against a world that no longer exists.
///
/// <b>Every method that opens a scope must await inside it.</b> Returning the task from a scope-opening
/// method disposes the context while the query is still running, and the driver reports that as
/// "BindComplete while expecting ReadyForQueryMessage" — a message naming neither the scope nor the
/// mistake. This harness produced that bug twice, in two different methods, which is the argument for
/// writing it down rather than fixing it once.
///
/// Each call creates a brand-new tenant rather than sharing one. Postings are journal-numbered, and a
/// test that asserted "the first number is 000001" against a tenant another test had already written to
/// would fail for a reason that had nothing to do with what it was checking.
/// </remarks>
internal sealed class LedgerHarness(WebAppFactory factory)
{
    /// <summary>
    /// The actor every test posting declares. A fixed id rather than a fresh one per call, so an
    /// assertion can check that what the audit row says matches who the caller claimed to be.
    /// </summary>
    internal static readonly Guid Actor = Guid.Parse("aaaaaaaa-0000-0000-0000-00000000000a");

    /// <summary>
    /// Creates a company the way the product does: provisioner, base chart, standard journals, twelve
    /// periods.
    /// </summary>
    /// <remarks>
    /// Kept apart from <see cref="ProvisionAsync"/>, which hand-builds a world. The invariant suite must
    /// not build its fixture with the code it is testing — if the provisioner were to write periods with
    /// wrong dates, a posting test using those periods would agree with it and stay green.
    /// </remarks>
    internal async Task<LedgerWorld> ProvisionedCompanyAsync(int fiscalYear = 2026)
    {
        var world = await ProvisionAsync(tenantOnly: true);

        using var scope = TenantScope.Open(
            factory.Services.GetRequiredService<IServiceScopeFactory>(), world.TenantId);

        await scope.ServiceProvider.GetRequiredService<ILedgerProvisioner>()
            .ProvisionAsync(fiscalYear);

        var cash = await FindAccountByCodeAsync(world.TenantId, "1010");
        var income = await FindAccountByCodeAsync(world.TenantId, "4010");

        return world with
        {
            Cash = cash,
            Income = income,
            PostingDate = new DateOnly(fiscalYear, 7, 1),
        };
    }

    /// <summary>
    /// Creates a tenant with a hand-built ledger: one open July period, two postable leaf accounts, one
    /// journal.
    /// </summary>
    /// <remarks>
    /// The fixture for the schema-level tests. Every value here is written directly so a test can be sure
    /// what the row says without trusting any product code to have produced it.
    /// </remarks>
    internal async Task<LedgerWorld> ProvisionAsync(bool tenantOnly = false)
    {
        var slug = "led-" + Guid.NewGuid().ToString("N")[..8];

        using (var systemScope = TenantScope.OpenSystem(
                   factory.Services.GetRequiredService<IServiceScopeFactory>()))
        {
            await systemScope.ServiceProvider.GetRequiredService<ITenantDirectory>().CreateAsync(new Tenant
            {
                Id = TenantId.New(),
                Slug = slug,
                DisplayName = slug,
            });
        }

        var tenantId = await ReadTenantIdAsync(slug);
        var start = new DateOnly(2026, 7, 1);

        if (tenantOnly)
        {
            return new LedgerWorld(tenantId, Guid.Empty, Guid.Empty, start);
        }

        using var scope = TenantScope.Open(
            factory.Services.GetRequiredService<IServiceScopeFactory>(), tenantId);

        var context = scope.ServiceProvider.GetRequiredService<OstagarDbContext>();

        var cash = NewAccount(tenantId, "1000", "Cash", AccountType.Asset, AccountSubType.Cash, "cash");
        var income = NewAccount(tenantId, "4000", "Sales Revenue", AccountType.Income, AccountSubType.SalesRevenue, "sales");

        context.Accounts.AddRange(cash, income);
        context.Journals.Add(new Journal
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Code = "GL",
            Name = "General",
            NumberPrefix = "GL",
        });
        context.FiscalPeriods.Add(new FiscalPeriod
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Number = 1,
            FiscalYear = 2026,
            StartDate = start,
            EndDate = start.AddMonths(1).AddDays(-1),
            Name = "July 2026",
        });

        await context.SaveChangesAsync();

        return new LedgerWorld(tenantId, cash.Id, income.Id, start);
    }

    /// <summary>Runs provisioning again for the same company, which is what an interrupted import does.</summary>
    /// <remarks>
    /// Awaiting rather than returning the task: the scope owns the context, and a method that hands back
    /// an unfinished task while its <c>using</c> disposes the scope mid-flight produces a protocol error
    /// from the driver that names neither the scope nor the mistake.
    /// </remarks>
    internal async Task ProvisionAgainAsync(LedgerWorld world, int fiscalYear)
    {
        using var scope = TenantScope.Open(
            factory.Services.GetRequiredService<IServiceScopeFactory>(), world.TenantId);

        await scope.ServiceProvider.GetRequiredService<ILedgerProvisioner>()
            .ProvisionAsync(fiscalYear);
    }

    internal async Task RecordRateAsync(
        LedgerWorld world,
        CurrencyCode currency,
        DateOnly validFrom,
        decimal rate)
    {
        using var scope = TenantScope.Open(
            factory.Services.GetRequiredService<IServiceScopeFactory>(), world.TenantId);

        var outcome = await scope.ServiceProvider.GetRequiredService<IExchangeRates>()
            .RecordAsync(new ExchangeRateQuote(currency, validFrom, rate), "test", Actor);

        Assert.True(
            outcome.Succeeded,
            $"Recording a {currency} rate for {validFrom:yyyy-MM-dd} was refused: "
            + string.Join("; ", outcome.Errors.Select(each => each.Message)));
    }

    internal async Task<PostingOutcome> TryRecordRateAsync(
        LedgerWorld world,
        CurrencyCode currency,
        DateOnly validFrom,
        decimal rate)
    {
        using var scope = TenantScope.Open(
            factory.Services.GetRequiredService<IServiceScopeFactory>(), world.TenantId);

        return await scope.ServiceProvider.GetRequiredService<IExchangeRates>()
            .RecordAsync(new ExchangeRateQuote(currency, validFrom, rate), "test", Actor);
    }

    internal async Task<RateOnLine> PostedLineAsync(LedgerWorld world, Guid entryId)
    {
        using var scope = TenantScope.Open(
            factory.Services.GetRequiredService<IServiceScopeFactory>(), world.TenantId);

        var context = scope.ServiceProvider.GetRequiredService<OstagarDbContext>();

        return await context.GlLines
            .Where(line => line.EntryId == entryId && line.Debit != null)
            .Select(line => new RateOnLine(line.ExchangeRate, line.AmountInCurrency, line.Debit ?? 0m))
            .SingleAsync();
    }

    /// <summary>Refused for the same reason a rate cannot be edited: the trail is history, not a setting.</summary>
    internal Task<string?> TryMutateRateAsync(LedgerWorld world, CurrencyCode currency, DateOnly validFrom)
    {
        return StoreErrorAsync(async () =>
        {
            using var scope = TenantScope.Open(
                factory.Services.GetRequiredService<IServiceScopeFactory>(), world.TenantId);

            var context = scope.ServiceProvider.GetRequiredService<OstagarDbContext>();

            await context.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE exchange_rates SET rate = 1 WHERE currency = {currency.Value} AND valid_from = {validFrom}");
        });
    }

    internal async Task<IReadOnlyList<AuditRow>> AuditRowsAsync(LedgerWorld world)
    {
        using var scope = TenantScope.Open(
            factory.Services.GetRequiredService<IServiceScopeFactory>(), world.TenantId);

        var context = scope.ServiceProvider.GetRequiredService<OstagarDbContext>();

        return await context.AuditLogs
            .OrderBy(entry => entry.Id)
            .Select(entry => new AuditRow(
                entry.Id, entry.Action, entry.Resource, entry.ResourceId,
                entry.Outcome, entry.ActorUserId, entry.ActorName, entry.Detail))
            .ToListAsync();
    }

    /// <summary>Attempts the one thing an append-only trail must not allow: rewriting a row.</summary>
    internal Task<string?> TryMutateAuditRowAsync(LedgerWorld world, bool delete)
    {
        return StoreErrorAsync(async () =>
        {
            using var scope = TenantScope.Open(
                factory.Services.GetRequiredService<IServiceScopeFactory>(), world.TenantId);

            var context = scope.ServiceProvider.GetRequiredService<OstagarDbContext>();

            var id = (await AuditRowsAsync(world))[0].Id;

            // Branching rather than a ternary on the interpolated string: the overload that escapes the
            // parameter takes a FormattableString, and a conditional of two interpolated strings is a
            // plain string, which reaches the raw-SQL overload with the value inlined.
            if (delete)
            {
                await context.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM audit_logs WHERE id = {id}");
            }
            else
            {
                await context.Database.ExecuteSqlInterpolatedAsync(
                    $"UPDATE audit_logs SET action = 'ledger.posting.forged' WHERE id = {id}");
            }
        });
    }

    /// <summary>
    /// Runs an action expected to fail, returning its exception chain, or null when it succeeded.
    /// </summary>
    internal static async Task<string?> StoreErrorAsync(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception exception)
        {
            return string.Join(" | ", Unwind(exception).Select(each => each.Message));
        }

        return null;
    }

    private static IEnumerable<Exception> Unwind(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            yield return current;
        }
    }

    /// <summary>
    /// A company with a real signed-in user, for the endpoint tests.
    /// </summary>
    /// <remarks>
    /// The user is created through <see cref="UserManager{T}"/> rather than inserted, because the roles
    /// that decide whether a clerk may post a journal are assigned by the same machinery the product
    /// uses. A row written straight into the database would grant a role the app never granted and the
    /// authorisation test would prove nothing.
    /// </remarks>
    internal async Task<HarnessCompany> ProvisionedCompanyWithUserAsync(string role)
    {
        var world = await ProvisionedCompanyAsync(2026);

        using (var scope = TenantScope.Open(
                   factory.Services.GetRequiredService<IServiceScopeFactory>(), world.TenantId))
        {
            await scope.ServiceProvider.GetRequiredService<PermissionSeeder>()
                .SeedTenantRolesAsync(world.TenantId);
        }

        return await SignInNewUserAsync(world, role, "actor");
    }

    /// <summary>
    /// Creates a second clerk inside an existing company and signs them in.
    /// </summary>
    /// <remarks>
    /// Separate from the company's first user because several tests need two roles in one company at the
    /// same time — a clerk who can post and a manager who can only read. Authorisation is the thing under
    /// test, so a second identity has to be a real second identity rather than the same token re-used.
    /// </remarks>
    internal Task<HarnessCompany> AddClerkAsync(LedgerWorld world, string role) =>
        SignInNewUserAsync(world, role, role.ToLowerInvariant());

    private async Task<HarnessCompany> SignInNewUserAsync(LedgerWorld world, string role, string prefix)
    {
        var slug = await SlugAsync(world.TenantId);
        var email = $"{prefix}@{slug}.test";

        using (var scope = TenantScope.Open(
                   factory.Services.GetRequiredService<IServiceScopeFactory>(), world.TenantId))
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

            var result = await users.CreateAsync(
                new ApplicationUser
                {
                    Id = Guid.NewGuid(),
                    TenantId = world.TenantId,
                    UserName = email,
                    Email = email,
                    FullName = role + " clerk",
                    IsActive = true,
                    CreatedAt = DateTimeOffset.UtcNow,
                },
                Password);

            Assert.True(result.Succeeded, string.Join("; ", result.Errors.Select(each => each.Description)));

            var created = await users.FindByEmailAsync(email);
            Assert.NotNull(created);

            var added = await users.AddToRoleAsync(created, role);
            Assert.True(added.Succeeded, string.Join("; ", added.Errors.Select(each => each.Description)));
        }

        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            TenantSlug = slug,
            Email = email,
            Password = Password,
        });

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<LoginPayload>();
        Assert.NotNull(payload);

        return new HarnessCompany(world, payload.Token, client);
    }

    internal async Task<string> SlugAsync(TenantId tenantId)
    {
        using var systemScope = TenantScope.OpenSystem(
            factory.Services.GetRequiredService<IServiceScopeFactory>());

        var tenant = await systemScope.ServiceProvider.GetRequiredService<ITenantDirectory>()
            .FindAsync(tenantId);

        Assert.NotNull(tenant);
        return tenant.Slug;
    }

    internal static HttpRequestMessage Authenticated(string token, HttpMethod method, string path)
    {
        var message = new HttpRequestMessage(method, path);
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return message;
    }

    internal static HttpRequestMessage Authenticated(string token, HttpMethod method, string path, object body)
    {
        var message = Authenticated(token, method, path);
        message.Content = JsonContent.Create(body);
        return message;
    }

    /// <summary>
    /// The next number of one named journal, which proves a refused posting cost nothing.
    /// </summary>
    /// <remarks>
    /// Scoped to a journal code because a provisioned company has six journals and each keeps its own
    /// sequence — that separation is the point of having six. Reading "the" next number across all of them
    /// is a <c>SingleAsync</c> that only worked while the fixture had one.
    /// </remarks>
    internal async Task<int> JournalNextNumberAsync(LedgerWorld world, string journalCode = "GL")
    {
        using var scope = TenantScope.Open(
            factory.Services.GetRequiredService<IServiceScopeFactory>(), world.TenantId);

        var context = scope.ServiceProvider.GetRequiredService<OstagarDbContext>();

        return await context.Journals
            .Where(each => each.Code == journalCode)
            .Select(each => each.NextNumber)
            .SingleAsync();
    }

    internal async Task<IReadOnlyList<string>> EntryNumbersAsync(LedgerWorld world)
    {
        using var scope = TenantScope.Open(
            factory.Services.GetRequiredService<IServiceScopeFactory>(), world.TenantId);

        var context = scope.ServiceProvider.GetRequiredService<OstagarDbContext>();

        return await context.GlEntries.OrderBy(each => each.Number).Select(each => each.Number).ToListAsync();
    }

    private const string Password = "Endpoint-suite-strong-password-42";

    internal async Task<Guid> FindAccountByCodeAsync(TenantId tenantId, string code)
    {
        using var scope = TenantScope.Open(
            factory.Services.GetRequiredService<IServiceScopeFactory>(), tenantId);

        var context = scope.ServiceProvider.GetRequiredService<OstagarDbContext>();

        return await context.Accounts
            .Where(account => account.Code == code)
            .Select(account => account.Id)
            .SingleAsync();
    }

    /// <summary>The company's whole chart as code, path, parent and postability — the template's shape.</summary>
    internal async Task<IReadOnlyList<AccountShape>> AccountsAsync(LedgerWorld world)
    {
        using var scope = TenantScope.Open(
            factory.Services.GetRequiredService<IServiceScopeFactory>(), world.TenantId);

        var context = scope.ServiceProvider.GetRequiredService<OstagarDbContext>();

        return await context.Accounts
            .OrderBy(account => account.Code)
            .Select(account => new AccountShape(
                account.Id, account.Code, account.Path, account.ParentId, account.IsPostable, account.IsActive))
            .ToListAsync();
    }

    internal async Task<IReadOnlyList<string>> JournalCodesAsync(LedgerWorld world)
    {
        using var scope = TenantScope.Open(
            factory.Services.GetRequiredService<IServiceScopeFactory>(), world.TenantId);

        var context = scope.ServiceProvider.GetRequiredService<OstagarDbContext>();

        return await context.Journals
            .OrderBy(journal => journal.Code)
            .Select(journal => journal.Code)
            .ToListAsync();
    }

    internal async Task<int> PeriodCountAsync(LedgerWorld world, int fiscalYear)
    {
        using var scope = TenantScope.Open(
            factory.Services.GetRequiredService<IServiceScopeFactory>(), world.TenantId);

        var context = scope.ServiceProvider.GetRequiredService<OstagarDbContext>();

        return await context.FiscalPeriods.CountAsync(each => each.FiscalYear == fiscalYear);
    }

    /// <summary>Which period and date an entry actually landed on.</summary>
    internal async Task<(int PeriodNumber, DateOnly PostingDate)> EntryPlacementAsync(
        LedgerWorld world,
        Guid entryId)
    {
        using var scope = TenantScope.Open(
            factory.Services.GetRequiredService<IServiceScopeFactory>(), world.TenantId);

        var context = scope.ServiceProvider.GetRequiredService<OstagarDbContext>();

        var entry = await context.GlEntries
            .Where(each => each.Id == entryId)
            .Select(each => new { each.PeriodId, each.PostingDate })
            .SingleAsync();

        var periodNumber = await context.FiscalPeriods
            .Where(each => each.Id == entry.PeriodId)
            .Select(each => each.Number)
            .SingleAsync();

        return (periodNumber, entry.PostingDate);
    }

    internal async Task<PostingOutcome> PostOpeningBalancesAsync(
        LedgerWorld world,
        int fiscalYear,
        IReadOnlyList<OpeningBalance> balances)
    {
        using var scope = TenantScope.Open(
            factory.Services.GetRequiredService<IServiceScopeFactory>(), world.TenantId);

        var service = scope.ServiceProvider.GetRequiredService<IOpeningBalances>();

        return await service.PostAsync(fiscalYear, balances, Actor);
    }

    /// <summary>
    /// Adds a non-postable parent account to a world, for the rule that grouping accounts are not
    /// somewhere money can be written.
    /// </summary>
    internal async Task<Guid> AddParentAccountAsync(LedgerWorld world)
    {
        using var scope = TenantScope.Open(
            factory.Services.GetRequiredService<IServiceScopeFactory>(), world.TenantId);

        var context = scope.ServiceProvider.GetRequiredService<OstagarDbContext>();

        var parent = new ChartOfAccount
        {
            Id = Guid.NewGuid(),
            TenantId = world.TenantId,
            Code = "100",
            Name = "Current Assets",
            Type = AccountType.Asset,
            SubType = AccountSubType.Cash,
            Path = "assets",
            IsPostable = false,
            IsActive = true,
        };

        context.Accounts.Add(parent);
        await context.SaveChangesAsync();

        return parent.Id;
    }

    /// <summary>An account id belonging to a different company, for cross-tenant refusal.</summary>
    internal async Task<Guid> ForeignAccountIdAsync()
    {
        var other = await ProvisionAsync();
        return other.Cash;
    }

    internal async Task LockPeriodAsync(LedgerWorld world)
    {
        using var scope = TenantScope.Open(
            factory.Services.GetRequiredService<IServiceScopeFactory>(), world.TenantId);

        var context = scope.ServiceProvider.GetRequiredService<OstagarDbContext>();

        // The period that actually contains this world's posting date. SingleAsync() was correct while the
        // hand-built fixture had one period and became wrong the moment provisioning created twelve: a
        // lock meant to close July would land on whichever period the query happened to return first.
        var period = await context.FiscalPeriods
            .Where(each => each.StartDate <= world.PostingDate && each.EndDate >= world.PostingDate)
            .SingleAsync();

        // Locked through its last day, which is the whole period. An open period accepts the identical
        // posting, and that is the positive control beside every refusal.
        period.SoftLockedOn = period.EndDate;

        await context.SaveChangesAsync();
    }

    /// <summary>Every journal number the tenant's entries carry, in the order they were written.</summary>
    internal async Task<IReadOnlyList<string>> NumbersAsync(LedgerWorld world)
    {
        using var scope = TenantScope.Open(
            factory.Services.GetRequiredService<IServiceScopeFactory>(), world.TenantId);

        var context = scope.ServiceProvider.GetRequiredService<OstagarDbContext>();

        return await context.GlEntries
            .OrderBy(entry => entry.Number)
            .Select(entry => entry.Number)
            .ToListAsync();
    }

    /// <summary>
    /// Posts a balanced pair through <see cref="Poster"/>, which is the path the product uses.
    /// </summary>
    /// <remarks>
    /// Returns the outcome rather than throwing on failure: several tests are about the refusals, and a
    /// helper that threw would hide the code that says which rule fired.
    /// </remarks>
    internal Task<PostingOutcome> PostAsync(
        LedgerWorld world,
        decimal amount = 500m,
        string? sourceType = "test.manual",
        Guid? sourceId = null,
        int iteration = 1,
        DateOnly? postingDate = null,
        Guid? debitAccountId = null,
        Guid? creditAccountId = null)
    {
        var request = new PostingRequest
        {
            JournalCode = "GL",
            ActorUserId = Actor,
            PostingDate = postingDate ?? world.PostingDate,
            SourceType = sourceType ?? "test.manual",
            SourceId = sourceId,
            Iteration = iteration,
            Memo = "written by the test suite",
            Lines =
            [
                Debit(debitAccountId ?? world.Cash, amount),
                Credit(creditAccountId ?? world.Income, amount),
            ],
        };

        return PostAsync(world, request);
    }

    internal async Task<PostingOutcome> PostAsync(LedgerWorld world, PostingRequest request)
    {
        using var scope = TenantScope.Open(
            factory.Services.GetRequiredService<IServiceScopeFactory>(), world.TenantId);

        var poster = scope.ServiceProvider.GetRequiredService<IPoster>();

        return await poster.PostAsync(request);
    }

    /// <summary>
    /// Writes an entry straight through the context, bypassing <see cref="IPoster"/>.
    /// </summary>
    /// <remarks>
    /// Only for the invariant suite, which must be able to build a posting the application layer would
    /// refuse before it ever reached the schema. Testing a database rule through a service that already
    /// enforces it proves the service, not the rule.
    /// </remarks>
    internal async Task WriteRawAsync(LedgerWorld world, Action<GlEntry> shape, bool post = true)
    {
        using var scope = TenantScope.Open(
            factory.Services.GetRequiredService<IServiceScopeFactory>(), world.TenantId);

        var context = scope.ServiceProvider.GetRequiredService<OstagarDbContext>();

        var period = await context.FiscalPeriods.SingleAsync();
        var number = (await context.GlEntries.CountAsync()) + 1;

        var entry = new GlEntry
        {
            Id = Guid.NewGuid(),
            TenantId = world.TenantId,
            Number = $"RAW-{number:000}",
            JournalCode = "GL",
            PeriodId = period.Id,
            PostingDate = world.PostingDate,
            SourceType = "test.raw",
            SourceId = Guid.NewGuid(),
            Memo = "written by the invariant suite",
            Status = GlEntryStatus.Draft,
        };

        shape(entry);

        foreach (var line in entry.Lines)
        {
            line.TenantId = world.TenantId;
        }

        context.GlEntries.Add(entry);

        // Two saves, because that is the protocol the immutability trigger imposes: a posted entry's
        // lines are untouchable, so they have to exist while the entry is still a draft.
        await context.SaveChangesAsync();

        if (!post)
        {
            return;
        }

        entry.Status = GlEntryStatus.Posted;
        await context.SaveChangesAsync();
    }

    internal static PostingLine Debit(Guid accountId, decimal amount) => new()
    {
        AccountId = accountId,
        Debit = Money.Bdt(amount),
        AmountInCurrency = amount,
        Currency = CurrencyCode.Bdt,
        ExchangeRate = 1m,
    };

    internal static PostingLine Credit(Guid accountId, decimal amount) => new()
    {
        AccountId = accountId,
        Credit = Money.Bdt(amount),
        AmountInCurrency = amount,
        Currency = CurrencyCode.Bdt,
        ExchangeRate = 1m,
    };

    internal static GlLine Line(Guid accountId, decimal debit = 0m, decimal credit = 0m) => new()
    {
        AccountId = accountId,
        LineOrder = debit > 0m ? 1 : 2,
        Debit = debit > 0m ? debit : null,
        Credit = credit > 0m ? credit : null,
        Currency = CurrencyCode.Bdt,
        AmountInCurrency = debit > 0m ? debit : credit,
        ExchangeRate = 1m,
    };

    /// <summary>An entry id belonging to this company, newest first.</summary>
    internal async Task<IReadOnlyList<Guid>> EntryIdsAsync(LedgerWorld world)
    {
        using var scope = TenantScope.Open(
            factory.Services.GetRequiredService<IServiceScopeFactory>(), world.TenantId);

        var context = scope.ServiceProvider.GetRequiredService<OstagarDbContext>();

        return await context.GlEntries
            .OrderByDescending(entry => entry.CreatedAt)
            .Select(entry => entry.Id)
            .ToListAsync();
    }

    /// <summary>
    /// What each account is left holding after every entry in the world, debit positive.
    /// </summary>
    /// <remarks>
    /// The check that a reversal actually undid something. Two balanced entries side by side are a
    /// balanced ledger whether or not the second one cancels the first, so "the trial balance still
    /// balances" is not evidence that a correction worked — only the per-account net is.
    /// </remarks>
    internal async Task<IReadOnlyDictionary<Guid, decimal>> NetByAccountAsync(LedgerWorld world)
    {
        using var scope = TenantScope.Open(
            factory.Services.GetRequiredService<IServiceScopeFactory>(), world.TenantId);

        var context = scope.ServiceProvider.GetRequiredService<OstagarDbContext>();

        var lines = await context.GlLines.ToListAsync();

        return lines
            .GroupBy(line => line.AccountId)
            .ToDictionary(
                group => group.Key,
                group => group.Sum(line => (line.Debit ?? 0m) - (line.Credit ?? 0m)));
    }

    internal async Task<PostingOutcome> ReverseAsync(
        LedgerWorld world,
        Guid entryId,
        string memo = "corrected by the test suite",
        DateOnly? reversalDate = null)
    {
        using var scope = TenantScope.Open(
            factory.Services.GetRequiredService<IServiceScopeFactory>(), world.TenantId);

        var poster = scope.ServiceProvider.GetRequiredService<IPoster>();

        return await poster.ReverseAsync(entryId, reversalDate ?? world.PostingDate, memo);
    }

    private async Task<TenantId> ReadTenantIdAsync(string slug)
    {
        using var systemScope = TenantScope.OpenSystem(
            factory.Services.GetRequiredService<IServiceScopeFactory>());

        var directory = systemScope.ServiceProvider.GetRequiredService<ITenantDirectory>();
        var tenant = await directory.FindBySlugAsync(slug);

        Assert.NotNull(tenant);
        return tenant.Id;
    }

    private static ChartOfAccount NewAccount(
        TenantId tenantId,
        string code,
        string name,
        AccountType type,
        AccountSubType subType,
        string path) => new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Code = code,
            Name = name,
            Type = type,
            SubType = subType,
            // A leaf: the table's own check refuses a postable account that has a parent.
            ParentId = null,
            Path = path,
            IsPostable = true,
            IsActive = true,
        };
}
