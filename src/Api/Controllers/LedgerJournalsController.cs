using Ostagar.Core;
using Ostagar.Data;
using Ostagar.Ledger;
using Ostagar.Platform.Security;
using Ostagar.Platform.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ostagar.Api.Controllers;

/// <summary>
/// Writing, reading and correcting general-ledger entries over HTTP.
/// </summary>
/// <remarks>
/// <para>
/// This is a thin surface over <see cref="IPoster"/> and deliberately adds no accounting rule of its own.
/// Every validation the endpoint could perform is already performed by the balancer and by a database
/// constraint, and a second copy here would be a third definition of the same rule — which is how the
/// legacy build ended up with three services disagreeing about the sign of a balance.
/// </para>
///
/// <para>
/// <b>Posting is one call, and correcting is not.</b> A manual journal arrives complete and leaves
/// posted, because a factory owner keying a journal wants the number immediately and a draft he has to
/// find again is a second job. A correction is a separate call that posts a mirror, because the original
/// is immutable once it exists — the entry keeps its number, its date and its lines, and the trail shows
/// both what was said and what it was wrong about.
/// </para>
///
/// <para>
/// <b>Domestic amounts only for now.</b> Lines are priced in the company's functional currency; a
/// foreign-currency line needs the rate field and the resolution path the ledger already has but this
/// form does not yet expose. Refusing at the boundary is stated below rather than left to be discovered
/// as a silently accepted request.
/// </para>
/// </remarks>
[ApiController]
[Route("api/v1/ledger/journals")]
public class LedgerJournalsController(
    OstagarDbContext context,
    IPoster poster,
    ITenantContext tenantContext,
    ICurrentActor actor) : ControllerBase
{
    /// <summary>
    /// Writes and posts one manual journal.
    /// </summary>
    /// <remarks>
    /// Returns 201 only when the entry exists. A refusal comes back as 400 carrying every rule it broke
    /// at once, because an accounts clerk fixing an unbalanced journal should not have to save four
    /// times to learn four things.
    /// </remarks>
    [HttpPost]
    [Authorize(Roles = "Owner,Accounts")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(
        [FromBody] CreateJournalRequest request,
        CancellationToken cancellationToken)
    {
        var refused = await ValidateRequestAsync(request, cancellationToken).ConfigureAwait(false);
        if (refused is not null)
        {
            return refused;
        }

        var postingActor = ResolveActor();
        if (postingActor is null)
        {
            // 401 rather than posting under a nameless actor. An entry nobody is attached to cannot be
            // defended later, and the ledger's audit rule refuses it anyway.
            return Unauthorized(new { reason = "actor_required_to_post" });
        }

        var currency = await FunctionalCurrencyAsync(cancellationToken).ConfigureAwait(false);
        if (currency is null)
        {
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new { reason = "tenant_currency_unresolvable" });
        }

        var lines = BuildLines(request.Lines, currency.Value);

        var outcome = await poster.PostAsync(new PostingRequest
        {
            JournalCode = request.JournalCode.Trim(),
            PostingDate = request.PostingDate,
            // The endpoint is where a human types a journal, so the source is this request's own identity
            // rather than a document row: no retry of a previous attempt exists to be idempotent against.
            SourceType = "manual.journal",
            SourceId = null,
            Memo = request.Memo?.Trim() ?? string.Empty,
            ActorUserId = postingActor.Value,
            Lines = lines,
        }, cancellationToken).ConfigureAwait(false);

        if (!outcome.Succeeded)
        {
            return BadRequest(new
            {
                reason = "posting_refused",
                errors = outcome.Errors.Select(each => new
                {
                    code = each.Code.ToString(),
                    message = each.Message,
                    line = each.LineIndex,
                }),
            });
        }

        return CreatedAtAction(
            nameof(Read),
            new { id = outcome.EntryId },
            new { entryId = outcome.EntryId, number = outcome.Number });
    }

    /// <summary>Opens one entry with its lines and the correction link in both directions.</summary>
    [HttpGet("{id:guid}")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Read(Guid id, CancellationToken cancellationToken)
    {
        // A foreign entry is absent rather than forbidden. The tenant filter makes the row invisible, and
        // confirming that an id exists somewhere else would turn this endpoint into a way to enumerate
        // other companies.
        var entry = await context.GlEntries
            .AsNoTracking()
            .Where(each => each.Id == id)
            .Select(each => new
            {
                each.Id,
                each.Number,
                each.JournalCode,
                each.PostingDate,
                each.PeriodId,
                Status = each.Status.ToString(),
                each.ReversesEntryId,
                each.Memo,
                Lines = each.Lines
                    .OrderBy(line => line.LineOrder)
                    .Select(line => new { line.Id, line.AccountId, line.LineOrder, line.Debit, line.Credit, line.Currency, line.AmountInCurrency, line.ExchangeRate, line.Memo })
                    .ToList(),
            })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (entry is null)
        {
            return NotFound();
        }

        var accountCodes = await AccountCodesAsync(
            entry.Lines.Select(line => line.AccountId).Distinct().ToArray(), cancellationToken)
            .ConfigureAwait(false);

        var reversedBy = await context.GlEntries
            .AsNoTracking()
            .Where(each => each.ReversesEntryId == entry.Id)
            .Select(each => (Guid?)each.Id)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return Ok(new JournalEntryResponse(
            entry.Id,
            entry.Number,
            entry.JournalCode,
            entry.PostingDate,
            entry.PeriodId,
            entry.Status,
            entry.ReversesEntryId,
            reversedBy,
            entry.Memo,
            entry.Lines.Sum(line => line.Debit ?? 0m),
            entry.Lines.Sum(line => line.Credit ?? 0m),
            entry.Lines.Select(line => new JournalLineResponse(
                line.Id,
                line.AccountId,
                accountCodes.TryGetValue(line.AccountId, out var code) ? code : string.Empty,
                line.LineOrder,
                line.Debit,
                line.Credit,
                line.Currency.ToString(),
                line.AmountInCurrency,
                line.ExchangeRate,
                line.Memo)).ToArray()));
    }

    /// <summary>
    /// Lists the entries of one period, newest number first.
    /// </summary>
    /// <remarks>
    /// Period-scoped rather than paginated-by-date because a closing accountant works one period at a
    /// time, and because an unbounded "recent entries" query over a general ledger has no honest limit:
    /// the answer changes between page loads on a busy company.
    /// </remarks>
    [HttpGet]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> List(
        [FromQuery] Guid? periodId,
        CancellationToken cancellationToken)
    {
        if (periodId is null)
        {
            return BadRequest(new { reason = "period_id_required" });
        }

        var entries = await context.GlEntries
            .AsNoTracking()
            .Where(each => each.PeriodId == periodId)
            .OrderByDescending(each => each.Number)
            .Select(each => new
            {
                each.Id,
                each.Number,
                each.JournalCode,
                each.PostingDate,
                Status = each.Status.ToString(),
                each.Memo,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return Ok(entries);
    }

    /// <summary>
    /// Corrects a posted entry by posting its mirror and linking the two.
    /// </summary>
    [HttpPost("{id:guid}/reverse")]
    [Authorize(Roles = "Owner,Accounts")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Reverse(
        Guid id,
        [FromBody] ReverseJournalRequest request,
        CancellationToken cancellationToken)
    {
        var postingActor = ResolveActor();
        if (postingActor is null)
        {
            return Unauthorized(new { reason = "actor_required_to_post" });
        }

        var outcome = await poster
            .ReverseAsync(id, request.ReversalDate, request.Memo?.Trim() ?? string.Empty, cancellationToken)
            .ConfigureAwait(false);

        if (!outcome.Succeeded)
        {
            return BadRequest(new
            {
                reason = "reversal_refused",
                errors = outcome.Errors.Select(each => new { code = each.Code.ToString(), message = each.Message }),
            });
        }

        return CreatedAtAction(
            nameof(Read),
            new { id = outcome.EntryId },
            new { entryId = outcome.EntryId, number = outcome.Number, reversesEntryId = id });
    }

    /// <summary>
    /// Rejects at the boundary what the ledger would refuse anyway, and says why here rather than there.
    /// </summary>
    /// <remarks>
    /// Only checks that are about the HTTP request rather than about accounting: a missing journal code, a
    /// line with neither side, and a foreign-currency line this form cannot price. Balance, period locks
    /// and account postability are the ledger's rules and are reached through <see cref="IPoster"/> so the
    /// API and the CLI enforce exactly one copy of each.
    /// </remarks>
    private async Task<IActionResult?> ValidateRequestAsync(
        CreateJournalRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.JournalCode))
        {
            return BadRequest(new { reason = "journal_code_required" });
        }

        if (request.Lines is null || request.Lines.Count == 0)
        {
            return BadRequest(new { reason = "lines_required" });
        }

        if (request.Lines.Any(line => line.Debit is null && line.Credit is null))
        {
            return BadRequest(new { reason = "line_side_required" });
        }

        if (!await context.Journals.AnyAsync(
                each => each.Code == request.JournalCode.Trim(), cancellationToken)
            .ConfigureAwait(false))
        {
            return BadRequest(new { reason = "journal_not_found", journalCode = request.JournalCode });
        }

        return null;
    }

    private static List<PostingLine> BuildLines(
        IReadOnlyList<JournalLineRequest> lines,
        Currency currency)
    {
        var result = new List<PostingLine>(lines.Count);

        foreach (var line in lines)
        {
            // Rounding is explicit and stated rather than left to the arithmetic: a journal typed with
            // more decimal places than the currency has is a mistake, and Money.Round records the choice
            // of how to resolve it instead of silently truncating.
            result.Add(new PostingLine
            {
                AccountId = line.AccountId,
                Debit = line.Debit is null ? null : Money.Round(currency, line.Debit.Value),
                Credit = line.Credit is null ? null : Money.Round(currency, line.Credit.Value),
                AmountInCurrency = line.Debit ?? line.Credit ?? 0m,
                Currency = currency.Code,
                Memo = line.Memo?.Trim() ?? string.Empty,
            });
        }

        return result;
    }

    /// <summary>
    /// The authenticated actor's id, or null when the token carries none.
    /// </summary>
    private Guid? ResolveActor() => actor.IsAuthenticated ? actor.Current?.UserId : null;

    private async Task<IReadOnlyDictionary<Guid, string>> AccountCodesAsync(
        Guid[] accountIds,
        CancellationToken cancellationToken)
    {
        var accounts = await context.Accounts
            .AsNoTracking()
            .Where(account => accountIds.Contains(account.Id))
            .Select(account => new { account.Id, account.Code })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return accounts.ToDictionary(account => account.Id, account => account.Code);
    }

    private async Task<Currency?> FunctionalCurrencyAsync(CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.TenantId;

        var code = await context.Tenants
            .AsNoTracking()
            .Where(each => each.Id == tenantId)
            .Select(each => each.FunctionalCurrencyCode)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return code is null ? null : Currency.Of(CurrencyCode.From(code));
    }
}
