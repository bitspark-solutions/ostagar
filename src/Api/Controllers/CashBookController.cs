using Garments.Core;
using Garments.Data;
using Garments.Ledger;
using Garments.Modules.CashBook;
using Garments.Platform.Security;
using Garments.Platform.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Garments.Api.Controllers;

/// <summary>
/// The daily cash book over HTTP: receipts and payments that post through the ledger, never beside it.
/// </summary>
/// <remarks>
/// <para>
/// Thin like <see cref="LedgerJournalsController"/>: every accounting rule lives in the cash book module
/// or the posting engine, and this surface only translates between HTTP and both. The legacy cash book
/// wrote a single-sided <c>JournalEntry</c> marked <c>Posted</c> directly; here a transaction that does
/// not produce a balanced posted entry is refused before any row exists.
/// </para>
///
/// <para>
/// <b>Posting precedes persistence, deliberately.</b> The ledger request carries the document's id as
/// its source key, so the entry is written first and the document saved only with the entry link in
/// hand. Saving the document first would leave a record of money nobody posted whenever the ledger
/// refused — a row claiming movement the books do not contain, which is the lie this endpoint exists
/// to make impossible. A retry after a crash re-presents the same source id, and the poster answers
/// "already posted" with the entry it wrote, so the document still lands exactly once.
/// </para>
/// </remarks>
[ApiController]
[Route("api/v1/cash-book/transactions")]
public class CashBookController(
    GarmentsDbContext context,
    IPoster poster,
    ICurrentActor actor) : ControllerBase
{
    private static readonly string[] AllowedKinds = ["receipt", "payment"];

    /// <summary>Records one receipt or payment and posts its entry.</summary>
    /// <remarks>
    /// Returns 201 only when both the ledger entry and the document row exist. A refusal comes back as
    /// 400 carrying the cash book's own errors and the ledger's posting errors together, because a
    /// clerk at the page should not have to submit once per mistake.
    /// </remarks>
    [HttpPost]
    [Authorize(Roles = "Owner,Accounts")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(
        [FromBody] CreateCashTransactionRequest request,
        CancellationToken cancellationToken)
    {
        var postingActor = actor.IsAuthenticated ? actor.Current?.UserId : null;
        if (postingActor is null)
        {
            return Unauthorized(new { reason = "actor_required_to_post" });
        }

        if (!Enum.TryParse<CashTransactionKind>(request.Kind, ignoreCase: true, out var kind))
        {
            return BadRequest(new { reason = "invalid_kind", allowed = AllowedKinds });
        }

        var cashAccount = await context.Accounts
            .AsNoTracking()
            .Where(each => each.Id == request.CashAccountId)
            .Select(each => new { each.Id, each.SubType })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (cashAccount is null)
        {
            // Absent rather than forbidden: the tenant filter makes another company's account invisible,
            // and confirming it exists elsewhere would be a way to enumerate across tenants.
            return BadRequest(new { reason = "cash_account_not_found" });
        }

        var currency = await ResolveCurrencyAsync(request.Currency, cancellationToken).ConfigureAwait(false);
        if (currency is null)
        {
            return BadRequest(new { reason = "currency_unresolvable" });
        }

        var draft = new NewCashTransaction
        {
            Kind = kind,
            TransactionDate = request.TransactionDate,
            CashAccountId = request.CashAccountId,
            CounterAccountId = request.CounterAccountId,
            Amount = Money.Round(currency.Value, request.Amount),
            PartnerId = request.PartnerId,
            Memo = request.Memo?.Trim() ?? string.Empty,
        };

        var validation = CashTransactionValidator.Validate(draft);
        if (!validation.Succeeded)
        {
            return Refused("transaction_refused", validation.Errors);
        }

        var transaction = new CashTransaction
        {
            Id = Guid.NewGuid(),
            Kind = kind,
            TransactionDate = draft.TransactionDate,
            CashAccountId = draft.CashAccountId,
            CounterAccountId = draft.CounterAccountId,
            Amount = draft.Amount!.Value.Amount,
            Currency = draft.Amount.Value.Currency.Code,
            PartnerId = draft.PartnerId,
            Memo = draft.Memo,
            ActorUserId = postingActor.Value,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        var plan = CashPosting.For(transaction, cashAccount.SubType);
        if (!plan.Succeeded)
        {
            return Refused("transaction_refused", plan.Errors);
        }

        var outcome = await poster.PostAsync(plan.Request!, cancellationToken).ConfigureAwait(false);
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

        transaction.PostedEntryId = outcome.EntryId;
        context.CashTransactions.Add(transaction);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return CreatedAtAction(
            nameof(Read),
            new { id = transaction.Id },
            new
            {
                transactionId = transaction.Id,
                entryId = outcome.EntryId,
                number = outcome.Number,
            });
    }

    /// <summary>Opens one transaction with its account codes and entry links resolved.</summary>
    [HttpGet("{id:guid}")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Read(Guid id, CancellationToken cancellationToken)
    {
        var transaction = await context.CashTransactions
            .AsNoTracking()
            .FirstOrDefaultAsync(each => each.Id == id, cancellationToken)
            .ConfigureAwait(false);

        if (transaction is null)
        {
            return NotFound();
        }

        return Ok(await ToResponseAsync(transaction, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>
    /// The day book: transactions between two dates, oldest first, in the order a paper cash book
    /// is read aloud.
    /// </summary>
    [HttpGet]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> List(
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        CancellationToken cancellationToken)
    {
        if (from is null || to is null)
        {
            return BadRequest(new { reason = "date_range_required" });
        }

        if (to < from)
        {
            return BadRequest(new { reason = "range_inverted" });
        }

        var rows = await context.CashTransactions
            .AsNoTracking()
            .Where(each => each.TransactionDate >= from && each.TransactionDate <= to)
            .OrderBy(each => each.TransactionDate)
            .ThenBy(each => each.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var codes = await AccountCodesAsync(
            rows.SelectMany(row => new[] { row.CashAccountId, row.CounterAccountId }).Distinct().ToArray(),
            cancellationToken).ConfigureAwait(false);

        var numbers = await EntryNumbersAsync(
            rows.Select(row => row.PostedEntryId).OfType<Guid>().ToArray(), cancellationToken)
            .ConfigureAwait(false);

        return Ok(rows.Select(row => ToResponse(row, codes, numbers)));
    }

    /// <summary>Corrects a posted transaction by posting the mirror entry and recording the link.</summary>
    [HttpPost("{id:guid}/reverse")]
    [Authorize(Roles = "Owner,Accounts")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Reverse(
        Guid id,
        [FromBody] ReverseJournalRequest request,
        CancellationToken cancellationToken)
    {
        var transaction = await context.CashTransactions
            .FirstOrDefaultAsync(each => each.Id == id, cancellationToken)
            .ConfigureAwait(false);

        if (transaction is null)
        {
            return NotFound();
        }

        var decision = CashTransactionValidator.CanReverse(transaction);
        if (!decision.Succeeded)
        {
            return Refused("reversal_refused", decision.Errors);
        }

        var outcome = await poster
            .ReverseAsync(
                transaction.PostedEntryId!.Value,
                request.ReversalDate,
                request.Memo?.Trim() ?? string.Empty,
                cancellationToken)
            .ConfigureAwait(false);

        if (!outcome.Succeeded)
        {
            return BadRequest(new
            {
                reason = "reversal_refused",
                errors = outcome.Errors.Select(each => new { code = each.Code.ToString(), message = each.Message }),
            });
        }

        transaction.Status = CashTransactionStatus.Reversed;
        transaction.ReversedByEntryId = outcome.EntryId;
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return CreatedAtAction(
            nameof(Read),
            new { id = transaction.Id },
            new { entryId = outcome.EntryId, number = outcome.Number, reversesEntryId = transaction.PostedEntryId });
    }

    private BadRequestObjectResult Refused(string reason, IReadOnlyList<CashTransactionError> errors) =>
        BadRequest(new
        {
            reason,
            errors = errors.Select(each => new { code = each.Code.ToString(), message = each.Message }),
        });

    private async Task<CashTransactionResponse> ToResponseAsync(
        CashTransaction transaction,
        CancellationToken cancellationToken)
    {
        var codes = await AccountCodesAsync(
            [transaction.CashAccountId, transaction.CounterAccountId], cancellationToken)
            .ConfigureAwait(false);

        var numbers = transaction.PostedEntryId is Guid entryId
            ? await EntryNumbersAsync([entryId], cancellationToken).ConfigureAwait(false)
            : new Dictionary<Guid, string>();

        return ToResponse(transaction, codes, numbers);
    }

    private static CashTransactionResponse ToResponse(
        CashTransaction transaction,
        IReadOnlyDictionary<Guid, string> accountCodes,
        IReadOnlyDictionary<Guid, string> entryNumbers) =>
        new(
            transaction.Id,
            transaction.Kind.ToString(),
            transaction.Status.ToString(),
            transaction.TransactionDate,
            transaction.CashAccountId,
            accountCodes.GetValueOrDefault(transaction.CashAccountId, string.Empty),
            transaction.CounterAccountId,
            accountCodes.GetValueOrDefault(transaction.CounterAccountId, string.Empty),
            transaction.Amount,
            transaction.Currency.ToString(),
            transaction.PartnerId,
            transaction.Memo,
            transaction.PostedEntryId,
            transaction.PostedEntryId is Guid id && entryNumbers.TryGetValue(id, out var number) ? number : null,
            transaction.ReversedByEntryId);

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

    private async Task<IReadOnlyDictionary<Guid, string>> EntryNumbersAsync(
        Guid[] entryIds,
        CancellationToken cancellationToken)
    {
        var entries = await context.GlEntries
            .AsNoTracking()
            .Where(entry => entryIds.Contains(entry.Id))
            .Select(entry => new { entry.Id, entry.Number })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return entries.ToDictionary(entry => entry.Id, entry => entry.Number);
    }

    private async Task<Currency?> ResolveCurrencyAsync(string? requested, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(requested))
        {
            return CurrencyCode.TryFrom(requested, out var parsed) ? Currency.Of(parsed) : null;
        }

        var code = await context.Tenants
            .AsNoTracking()
            .Select(each => each.FunctionalCurrencyCode)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return code is null ? null : Currency.Of(CurrencyCode.From(code));
    }
}
