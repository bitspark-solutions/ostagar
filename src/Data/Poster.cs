using System.Globalization;
using Ostagar.Core;
using Ostagar.Ledger;
using Ostagar.Platform.Audit;
using Ostagar.Platform.Security;
using Ostagar.Platform.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace Ostagar.Data;

/// <summary>
/// Turns a <see cref="PostingRequest"/> into a posted general-ledger entry.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why the work happens in this order.</b> Every rule is checked before anything is written, and the
/// journal number is taken last. A rejected posting must not consume a number, because invariant I4
/// promises the numbering has no gaps — and a gap whose cause was a validation failure nobody remembers
/// is indistinguishable from a deleted entry when an auditor asks.
/// </para>
///
/// <para>
/// <b>Two saves inside one transaction.</b> The immutability trigger reads the entry's status to decide
/// whether a line may change, and a posted entry's lines are untouchable, so the lines have to be written
/// while the entry is still a draft. Both saves share a transaction so the number allocation rolls back
/// with the posting it belonged to: incrementing outside it would leave a hole in the journal every time
/// a commit failed.
/// </para>
///
/// <para>
/// <b>The database still wins.</b> Everything validated here is validated again by a constraint or a
/// trigger, and a refusal from Postgres is translated rather than allowed to escape as a stack trace.
/// Law L4 is that invariants live in the schema; this class exists to give a person a readable reason,
/// not to be the thing that protects the ledger.
/// </para>
/// </remarks>
public sealed class Poster(
    OstagarDbContext context,
    ITenantContext tenantContext,
    ICurrentActor actor,
    IExchangeRates rates,
    IAuditWriter audit) : IPoster
{
    private const string PostAction = "ledger.posting.post";
    private const string RetryAction = "ledger.posting.retry";
    private const string RejectAction = "ledger.posting.reject";
    private const string GlEntryResource = "gl_entry";

    public async Task<PostingOutcome> PostAsync(
        PostingRequest request,
        CancellationToken cancellationToken = default)
    {
        var tenantId = tenantContext.TenantId;

        if (tenantId.IsEmpty)
        {
            throw new InvalidOperationException(
                "A posting needs a resolved tenant. Callers reach this through a tenant scope; arriving "
                + "without one is a wiring mistake, not a user error, and refusing here keeps it that way.");
        }

        var (lines, rateRefusal) = await ResolveRatesAsync(request, cancellationToken).ConfigureAwait(false);
        if (rateRefusal is not null)
        {
            return await RecordAsync(rateRefusal, request, AuditOutcome.Failed, cancellationToken)
                .ConfigureAwait(false);
        }

        request = request with { Lines = lines };

        var balance = PostingBalance.Check(request.Lines.ToList());
        if (!balance.Succeeded)
        {
            return await RefuseAsync(request, balance.Errors, cancellationToken).ConfigureAwait(false);
        }

        var journal = await context.Journals
            .FirstOrDefaultAsync(each => each.Code == request.JournalCode, cancellationToken)
            .ConfigureAwait(false);

        if (journal is null)
        {
            return await RefuseAsync(request, [new PostingError(
                PostingErrorCode.JournalNotFound,
                $"This company has no journal called '{request.JournalCode}'.")], cancellationToken)
                .ConfigureAwait(false);
        }

        var refused = await RefuseUnpostableAccountsAsync(request, cancellationToken).ConfigureAwait(false);
        if (refused is not null)
        {
            return await RecordAsync(refused, request, AuditOutcome.Failed, cancellationToken).ConfigureAwait(false);
        }

        var (period, periodRefusal) = await FindOpenPeriodAsync(request, cancellationToken).ConfigureAwait(false);
        if (periodRefusal is not null)
        {
            return await RecordAsync(periodRefusal, request, AuditOutcome.Failed, cancellationToken)
                .ConfigureAwait(false);
        }

        var existing = await FindExistingAsync(request, cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            // Recorded, because "the same document was submitted twice" is precisely the fact an auditor
            // of a duplicated payment wants to see, and a retry that leaves no trace is indistinguishable
            // from a retry that never arrived.
            return await RecordAsync(
                new PostingOutcome(PostingOutcomeStatus.AlreadyPosted, [], existing.Id, existing.Number),
                request,
                AuditOutcome.Allowed,
                cancellationToken,
                RetryAction).ConfigureAwait(false);
        }

        await using var transaction = await context.Database
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);

        var number = $"{journal.NumberPrefix}-{await AllocateNumberAsync(journal, cancellationToken)
            .ConfigureAwait(false):000000}";

        var entry = BuildEntry(request, tenantId, period!.Id, number);

        for (var index = 0; index < request.Lines.Count; index++)
        {
            entry.Lines.Add(BuildLine(request.Lines[index], tenantId, entry.Id, index + 1));
        }

        context.GlEntries.Add(entry);

        try
        {
            // Draft first: the lines are written while the entry can still be shaped, and the flip below
            // is the single act that makes it permanent.
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            entry.Status = GlEntryStatus.Posted;

            // Appended before the save that flips the status, so the audit row and the posting are written by
            // the same commit. This is the one place the ordering matters: append after the commit and a
            // failure between the two leaves a posted entry nobody recorded posting.
            AppendAuditRecord(entry.Id, number, request, AuditOutcome.Allowed, PostAction, null);

            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (IsDatabaseRefusal(exception) || IsIdempotencyCollision(exception))
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);

            // The rollback discarded the staged audit row along with the entry. The tracker still holds
            // both, so saving without clearing it would re-attempt the posting the database just refused
            // and fail on it a second time.
            context.ChangeTracker.Clear();

            if (IsIdempotencyCollision(exception))
            {
                // Two requests carrying the same source key raced past FindExistingAsync and the unique
                // index picked the winner. The committed entry is the answer both callers were asking
                // for, so the loser is a retry rather than a refusal — and it is still written down.
                var committed = await FindExistingAsync(request, cancellationToken).ConfigureAwait(false);
                if (committed is not null)
                {
                    return await RecordAsync(
                        new PostingOutcome(PostingOutcomeStatus.AlreadyPosted, [], committed.Id, committed.Number),
                        request,
                        AuditOutcome.Allowed,
                        cancellationToken,
                        RetryAction).ConfigureAwait(false);
                }
            }

            // The balance trigger is deferred to commit, so this is where an entry the application
            // wrongly accepted is actually stopped — and where the number it took is given back.
            IReadOnlyList<PostingError> errors = [new PostingError(
                PostingErrorCode.RefusedByDatabase,
                $"The database refused this posting: {RootMessage(exception)}")];

            return await RefuseAsync(request, errors, cancellationToken).ConfigureAwait(false);
        }

        return new PostingOutcome(PostingOutcomeStatus.Posted, [], entry.Id, number);
    }

    public async Task<PostingOutcome> ReverseAsync(
        Guid entryId,
        DateOnly reversalDate,
        string memo,
        CancellationToken cancellationToken = default)
    {
        var original = await context.GlEntries
            .Include(each => each.Lines)
            .FirstOrDefaultAsync(each => each.Id == entryId, cancellationToken)
            .ConfigureAwait(false);

        if (original is null)
        {
            return new PostingOutcome(PostingOutcomeStatus.Rejected, [new PostingError(
                PostingErrorCode.EntryNotFound,
                "There is no entry here with that id.")]);
        }

        if (original.Status != GlEntryStatus.Posted)
        {
            return new PostingOutcome(PostingOutcomeStatus.Rejected, [new PostingError(
                PostingErrorCode.EntryNotPosted,
                $"{original.Number} is still a draft. Edit it instead of reversing it — a reversal "
                + "exists to correct a fact the ledger has already stated.")]);
        }

        // Checked before the mirror is built so a double click cannot post two corrections. The partial
        // unique index in the enforcement migration is the same rule; this is the readable version of it,
        // and the race the index exists to lose is one user's second click.
        var alreadyReversed = await context.GlEntries
            .AnyAsync(each => each.ReversesEntryId == entryId, cancellationToken)
            .ConfigureAwait(false);

        if (alreadyReversed)
        {
            return new PostingOutcome(PostingOutcomeStatus.Rejected, [new PostingError(
                PostingErrorCode.AlreadyReversed,
                $"{original.Number} has already been reversed. A second reversal would double the "
                + "correction while leaving both entries looking like the single one intended.")]);
        }

        var mirrored = original.Lines
            .OrderBy(line => line.LineOrder)
            .Select(Mirror)
            .ToList();

        return await PostAsync(new PostingRequest
        {
            JournalCode = original.JournalCode,
            PostingDate = reversalDate,
            // Keyed on the entry being corrected, so the database's idempotency index makes a retried
            // reversal the same posting rather than a second one.
            SourceType = "ledger.reversal",
            SourceId = original.Id,
            Memo = memo,
            ReversesEntryId = original.Id,
            // The person clicking "reverse" is the actor, not whoever posted the original — but when no
            // request is in flight there is nobody to ask, so the entry's own posted-by is the last
            // defensible answer and an empty Guid would be nobody at all.
            ActorUserId = actor.Current?.UserId ?? original.PostedByUserId ?? original.CreatedByUserId
                ?? throw new InvalidOperationException(
                    $"Reversing entry {original.Id} needs an actor, and the entry records none. Nothing "
                    + "may be written to the ledger against no name."),
            Lines = mirrored,
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Confirms every account is one this tenant owns and can actually be posted to.
    /// </summary>
    /// <remarks>
    /// One query for the whole set rather than one per line: the failure being guarded against — a line
    /// mapped at a parent account, which is a grouping and not somewhere to post — is the same on the
    /// twentieth line as on the first, and twenty round trips to find that out is twenty more chances for
    /// the answer to change underneath.
    ///
    /// The tenant filter on <see cref="OstagarDbContext"/> makes "not mine" and "does not exist" the same
    /// answer here, which is the intended shape: a guessed account id from another company must not be
    /// distinguishable from a typo.
    /// </remarks>
    private async Task<PostingOutcome?> RefuseUnpostableAccountsAsync(
        PostingRequest request,
        CancellationToken cancellationToken)
    {
        var ids = request.Lines.Select(line => line.AccountId).Distinct().ToArray();

        var found = await context.Accounts
            .Where(account => ids.Contains(account.Id))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var byId = found.ToDictionary(account => account.Id);

        // Registered banks carry their own currency: a statement is that account's movements in that
        // currency, so a line in any other currency would post to the ledger and never appear on one.
        // The gl_lines trigger is the rule; this is the readable version of it.
        var bankCurrencies = await context.BankAccounts
            .Where(bank => ids.Contains(bank.LedgerAccountId))
            .ToDictionaryAsync(bank => bank.LedgerAccountId, bank => bank.Currency, cancellationToken)
            .ConfigureAwait(false);

        for (var index = 0; index < request.Lines.Count; index++)
        {
            if (!byId.TryGetValue(request.Lines[index].AccountId, out var account))
            {
                return new PostingOutcome(PostingOutcomeStatus.Rejected, [new PostingError(
                    PostingErrorCode.AccountUnpostable,
                    "A line uses an account this company does not have.",
                    index)]);
            }

            if (!account.IsPostable || !account.IsActive)
            {
                return new PostingOutcome(PostingOutcomeStatus.Rejected, [new PostingError(
                    PostingErrorCode.AccountUnpostable,
                    $"'{account.Code} {account.Name}' cannot be posted to because "
                    + $"{(account.IsPostable ? "it is inactive" : "it is a parent account, not a leaf")}.",
                    index)]);
            }

            if (bankCurrencies.TryGetValue(account.Id, out var bankCurrency)
                && request.Lines[index].Currency != bankCurrency)
            {
                return new PostingOutcome(PostingOutcomeStatus.Rejected, [new PostingError(
                    PostingErrorCode.BankCurrencyMismatch,
                    $"'{account.Code} {account.Name}' is registered as a {bankCurrency} bank account, "
                    + $"so a {request.Lines[index].Currency} line on it would never appear on its statement.",
                    index)]);
            }
        }

        return null;
    }

    private async Task<(FiscalPeriod? Period, PostingOutcome? Refusal)> FindOpenPeriodAsync(
        PostingRequest request,
        CancellationToken cancellationToken)
    {
        var period = await context.FiscalPeriods
            .Where(each => each.StartDate <= request.PostingDate && each.EndDate >= request.PostingDate)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (period is null)
        {
            return (null, new PostingOutcome(PostingOutcomeStatus.Rejected, [new PostingError(
                PostingErrorCode.PeriodNotFound,
                $"No fiscal period covers {request.PostingDate:yyyy-MM-dd}. "
                + "Provision the company's year before posting into it.")]));
        }

        if (!period.AcceptsPosting(request.PostingDate))
        {
            var lockedThrough = period.HardLockedOn ?? period.TaxLockedOn ?? period.SoftLockedOn;

            return (null, new PostingOutcome(PostingOutcomeStatus.Rejected, [new PostingError(
                PostingErrorCode.PeriodLocked,
                $"Period {period.Number} of fiscal year {period.FiscalYear} is locked through "
                + $"{lockedThrough:yyyy-MM-dd}, so {request.PostingDate:yyyy-MM-dd} cannot be posted into it. "
                + "Lifting a lock is a separate act with its own audit entry.")]));
        }

        return (period, null);
    }

    private async Task<GlEntry?> FindExistingAsync(PostingRequest request, CancellationToken cancellationToken)
    {
        if (request.SourceId is null)
        {
            return null;
        }

        return await context.GlEntries
            .FirstOrDefaultAsync(
                entry => entry.SourceType == request.SourceType
                    && entry.SourceId == request.SourceId
                    && entry.Iteration == request.Iteration,
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Takes the next journal number, returning the number the entry should carry.
    /// </summary>
    /// <remarks>
    /// A single <c>UPDATE</c> that increments, then a reload of the locked row — not a read followed by a
    /// write. Two postings reading <c>next_number</c> before either wrote would each receive the same
    /// number, and the unique index would reject the second as a duplicate: a collision that looks like a
    /// bug in the index and is really two writers trusting a value each read too early. The increment
    /// takes the row lock and holds it until this transaction ends, and the reload reads
    /// <c>next_number - 1</c> because after the increment the column names the number <i>after</i> the one
    /// just handed out.
    /// </remarks>
    private async Task<int> AllocateNumberAsync(Journal journal, CancellationToken cancellationToken)
    {
        var consumed = await context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE journals SET next_number = next_number + 1 WHERE id = {journal.Id}",
            cancellationToken)
            .ConfigureAwait(false);

        if (consumed != 1)
        {
            throw new InvalidOperationException(
                $"Allocating a number for journal '{journal.Code}' updated {consumed} rows, and the journal "
                + "row that should have been locked is not the one this posting read.");
        }

        await context.Entry(journal).ReloadAsync(cancellationToken).ConfigureAwait(false);

        return journal.NextNumber - 1;
    }

    private static GlEntry BuildEntry(PostingRequest request, TenantId tenantId, Guid periodId, string number) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = tenantId,
        Number = number,
        JournalCode = request.JournalCode,
        PeriodId = periodId,
        PostingDate = request.PostingDate,
        CreatedAt = DateTimeOffset.UtcNow,
        Memo = request.Memo,
        SourceType = request.SourceType,
        SourceId = request.SourceId,
        Iteration = request.Iteration,
        ReversesEntryId = request.ReversesEntryId,
        Status = GlEntryStatus.Draft,
        CreatedByUserId = request.ActorUserId,
        PostedByUserId = request.ActorUserId,
    };

    /// <summary>
    /// Flips one line to the other side, changing nothing else about it.
    /// </summary>
    /// <remarks>
    /// The account, currency, current-currency amount and rate all carry over untouched. Only the side
    /// moves: a reversal that also re-derived the exchange difference would be a second, different
    /// correction riding along with the one that was asked for.
    /// </remarks>
    private static PostingLine Mirror(GlLine source)
    {
        var currency = Currency.Of(source.Currency);

        return new PostingLine
        {
            AccountId = source.AccountId,
            Debit = source.Credit is null ? null : new Money(currency, source.Credit.Value),
            Credit = source.Debit is null ? null : new Money(currency, source.Debit.Value),
            Currency = source.Currency,
            AmountInCurrency = source.AmountInCurrency,
            ExchangeRate = source.ExchangeRate,
            ExchangeRateDate = source.ExchangeRateDate,
            AnalysisId = source.AnalysisId,
            Memo = source.Memo,
        };
    }

    private static GlLine BuildLine(PostingLine source, TenantId tenantId, Guid entryId, int lineOrder) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = tenantId,
        EntryId = entryId,
        AccountId = source.AccountId,
        LineOrder = lineOrder,
        Debit = source.Debit?.Amount,
        Credit = source.Credit?.Amount,
        Currency = source.Currency,
        AmountInCurrency = source.AmountInCurrency,
        // Par for a line already stated in functional currency. The resolution step has guaranteed a real
        // rate for anything foreign before this runs, so a missing value here can only mean domestic, and
        // the ledger row always stores a definite number the schema can constrain.
        ExchangeRate = source.ExchangeRate ?? 1m,
        ExchangeRateDate = source.ExchangeRateDate,
        AnalysisId = source.AnalysisId,
        Memo = source.Memo,
    };

    /// <summary>
    /// Resolves any foreign-currency rate the caller left to the ledger, then checks that the taka figure
    /// on each foreign line actually follows from its own currency and rate.
    /// </summary>
    /// <remarks>
    /// The caller states both amounts and the ledger decides which rate applies and whether the two agree.
    /// Deriving the functional amount here instead would quietly discard a number the caller deliberately
    /// typed, which is the kind of helpfulness that makes a document and its posting disagree with nobody
    /// able to say who decided it. A UI shows the rate and the user confirms it; this checks that what was
    /// confirmed is what the ledger's own history implies.
    /// </remarks>
    /// <remarks>
    /// Resolution happens before balancing rather than during it. The balancer's job is to say whether a
    /// complete posting is valid; a line whose rate has not been looked up yet is not a complete posting,
    /// and letting the balancer reach out to a table would make a pure check depend on data it is not
    /// supposed to know about.
    ///
    /// The consistency test is the point of the exercise. Storing a rate and never using it to check the
    /// amount beside it leaves a line that claims 20.00 USD, 112.40, and 2,300.00 taka when 20 x 112.40
    /// is 2,248.00 — an FX difference that exists in no account, which is precisely what invariant I7
    /// exists to prevent.
    /// </remarks>
    private async Task<(IReadOnlyList<PostingLine> Lines, PostingOutcome? Refusal)> ResolveRatesAsync(
        PostingRequest request,
        CancellationToken cancellationToken)
    {
        var resolved = new List<PostingLine>(request.Lines.Count);
        var errors = new List<PostingError>();

        for (var index = 0; index < request.Lines.Count; index++)
        {
            var line = request.Lines[index];

            // The Money's own currency is the functional one, so a line is foreign when the currency it
            // names differs from it. Asking the tenant for its functional currency here would be a
            // second, possibly disagreeing answer to a question the line already states.
            var side = line.Debit ?? line.Credit;

            // A line with no amount cannot be priced, and the balancer already reports it. Same for a
            // line whose own currency is the one its Money is stated in.
            if (side is null || line.Currency == side.Value.Currency.Code)
            {
                resolved.Add(line);
                continue;
            }

            var functional = side.Value.Currency;
            decimal rate;

            if (line.ExchangeRate is null)
            {
                var quote = await rates.FindAsync(line.Currency, request.PostingDate, cancellationToken)
                    .ConfigureAwait(false);

                if (quote is null)
                {
                    errors.Add(new PostingError(
                        PostingErrorCode.MissingExchangeRate,
                        $"This company has no {line.Currency} rate effective on or before "
                        + $"{request.PostingDate:yyyy-MM-dd}.",
                        index));
                    continue;
                }

                rate = quote.Rate;
                resolved.Add(line with { ExchangeRate = rate });
            }
            else
            {
                rate = line.ExchangeRate.Value;
                resolved.Add(line);
            }

            if (rate <= 0m)
            {
                continue;
            }

            var expected = Money.Round(functional, line.AmountInCurrency * rate);

            if (expected.Amount != side.Value.Amount)
            {
                errors.Add(new PostingError(
                    PostingErrorCode.ExchangeRateAmountMismatch,
                    // Formatted invariantly: this text ends up in an audit row and, in a support ticket,
                    // copied across machines whose default thousands separator differs.
                    $"Line {index + 1} states "
                    + $"{line.AmountInCurrency.ToString("0.####", CultureInfo.InvariantCulture)} {line.Currency} at "
                    + $"{rate.ToString("0.##########", CultureInfo.InvariantCulture)}, which is "
                    + $"{expected.Amount.ToString("0.00", CultureInfo.InvariantCulture)}, but the posting carries "
                    + $"{side.Value.Amount.ToString("0.00", CultureInfo.InvariantCulture)}.",
                    index));
            }
        }

        return errors.Count == 0
            ? (resolved, null)
            : (resolved, new PostingOutcome(PostingOutcomeStatus.Rejected, errors));
    }

    /// <summary>
    /// Records a refusal and returns it.
    /// </summary>
    /// <remarks>
    /// A rejected attempt is written down on purpose. An audit trail that only contains successes cannot
    /// answer the question an auditor actually asks — how many times did someone try to post this, and
    /// why did it not go through — and a failed posting is the one event where a person was about to
    /// change the ledger and did not.
    /// </remarks>
    private async Task<PostingOutcome> RefuseAsync(
        PostingRequest request,
        IReadOnlyList<PostingError> errors,
        CancellationToken cancellationToken) =>
        await RecordAsync(
            new PostingOutcome(PostingOutcomeStatus.Rejected, errors),
            request,
            AuditOutcome.Failed,
            cancellationToken,
            RejectAction)
            .ConfigureAwait(false);

    private async Task<PostingOutcome> RecordAsync(
        PostingOutcome outcome,
        PostingRequest request,
        AuditOutcome auditOutcome,
        CancellationToken cancellationToken,
        string action = PostAction)
    {
        AppendAuditRecord(outcome.EntryId, outcome.Number, request, auditOutcome, action, outcome.Errors);

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return outcome;
    }

    private void AppendAuditRecord(
        Guid? entryId,
        string? number,
        PostingRequest request,
        AuditOutcome outcome,
        string action,
        IReadOnlyList<PostingError>? errors)
    {
        // ResourceId stays empty when nothing was written. A refusal has no row to name, and inventing an
        // id for it would give the trail an entry that points nowhere — the same shape as the fabricated
        // identifiers law L3 exists to forbid, arriving from the logging side instead.
        // The id is the one the caller declared and always arrives. The name, correlation and address
        // are enriched from the ambient request when there is one: outside HTTP, such as a CLI import,
        // there is no principal to read them from, and inventing a display name would put words in a
        // mouth that was not present. The id is what the trail is legally read for.
        audit.Append(new AuditRecord(
            ActorUserId: request.ActorUserId,
            ActorName: actor.Current?.Name ?? string.Empty,
            Action: action,
            Resource: GlEntryResource,
            ResourceId: entryId?.ToString("D") ?? string.Empty,
            Outcome: outcome,
            CorrelationId: actor.CorrelationId,
            SourceAddress: actor.SourceAddress,
            Detail: new
            {
                journal = request.JournalCode,
                postingDate = request.PostingDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                sourceType = request.SourceType,
                sourceId = request.SourceId,
                lineCount = request.Lines.Count,
                entryNumber = number,
                refusals = errors?.Select(each => each.Code.ToString()).ToArray(),
            }));
    }

    /// <summary>
    /// Whether an exception is the database enforcing a rule, as opposed to the process failing.
    /// </summary>
    /// <remarks>
    /// Deliberately narrow, and keyed on SQLSTATE <c>P0001</c> — the code every <c>RAISE EXCEPTION</c> in
    /// this schema carries — rather than on a message prefix. A lost connection is not a refused posting,
    /// and reporting it as one would tell the caller their document is invalid when nothing was written
    /// and the ledger is fine, which is how someone re-keys an entry that already saved.
    /// </remarks>
    private static bool IsDatabaseRefusal(Exception exception) =>
        Unwind(exception).Any(each => each is Npgsql.PostgresException postgres
            && postgres.SqlState == "P0001");

    /// <summary>
    /// Whether an exception is the idempotency index firing — SQLSTATE 23505 on
    /// <c>UQ_gl_entries_source_key</c> and nothing else. Naming the constraint matters: a uniqueness
    /// failure on the entry number or any other index is a genuine fault, not a duplicate retry, and
    /// reporting it as <c>AlreadyPosted</c> would point a caller at an entry that is not theirs.
    /// </summary>
    private static bool IsIdempotencyCollision(Exception exception) =>
        Unwind(exception).Any(each => each is Npgsql.PostgresException postgres
            && postgres.SqlState == Npgsql.PostgresErrorCodes.UniqueViolation
            && postgres.ConstraintName == "UQ_gl_entries_source_key");

    private static string RootMessage(Exception exception) => Unwind(exception).Last().Message;

    private static IEnumerable<Exception> Unwind(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            yield return current;
        }
    }
}
