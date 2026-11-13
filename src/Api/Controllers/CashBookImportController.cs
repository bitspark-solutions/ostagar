using Ostagar.Core;
using Ostagar.Data;
using Ostagar.Ledger;
using Ostagar.Modules.CashBook;
using Ostagar.Platform.Audit;
using Ostagar.Platform.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ostagar.Api.Controllers;

/// <summary>
/// CSV import of the client's two-sided cash book over HTTP — the P3 gate item.
/// </summary>
/// <remarks>
/// <para>
/// Thin like <see cref="CashBookController"/>: the file format lives in
/// <see cref="CashBookImportParser"/>, category mapping in <see cref="CashBookImportMapper"/>, the
/// posting shape in <see cref="CashPosting"/>, and every accounting rule in <see cref="IPoster"/>.
/// This controller only resolves the caller's explicit category → account-code mapping against the
/// tenant's chart and carries each row-side through the same pipeline the manual form uses — post
/// first, document second — so an imported file cannot produce the single-sided <c>Posted</c> rows
/// the legacy import wrote.
/// </para>
///
/// <para>
/// <b>Idempotency is per file, by content.</b> Each row-side's document id is
/// <see cref="CashBookImportKey.TransactionId"/> — derived from the file fingerprint, the line number
/// and the side — and that id is also the posting's source id under source type
/// <see cref="CashSourceTypes.Import"/>. Re-uploading the same bytes resolves every row to
/// "already posted" and writes nothing; uploading an edited file is a new import, because changed
/// bytes are genuinely different content.
/// </para>
///
/// <para>
/// <b>Partial success is the intended outcome.</b> A refused row does not fail the file: the response
/// reports each line as posted, already-posted or refused with reasons, because a hand-typed book with
/// one bad date still moved real money on its other rows.
/// </para>
/// </remarks>
[ApiController]
[Route("api/v1/cash-book/import")]
public class CashBookImportController(
    OstagarDbContext context,
    IPoster poster,
    IAuditWriter audit,
    ICurrentActor actor) : ControllerBase
{
    private const string ImportAction = "cash.import";
    private const string ImportResource = "cash_import";

    /// <summary>
    /// The largest upload accepted, in characters. A year's book is well under a megabyte; the bound
    /// exists so a posting endpoint cannot be turned into a memory sink.
    /// </summary>
    private const int MaxContentChars = 4_000_000;

    /// <summary>Imports one cash-book CSV, posting every parsable row through the ledger.</summary>
    /// <remarks>
    /// Returns 200 with the per-row outcome summary even when every row refused — the request itself
    /// was well-formed and the summary is the answer. A 400 means the request could not be processed
    /// at all: no content, no such cash account, an unresolvable currency.
    /// </remarks>
    [HttpPost]
    [Authorize(Roles = "Owner,Accounts")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Import(
        [FromBody] ImportCashBookRequest request,
        CancellationToken cancellationToken)
    {
        var identity = actor.IsAuthenticated ? actor.Current : null;
        if (identity is null)
        {
            return Unauthorized(new { reason = "actor_required_to_post" });
        }

        if (string.IsNullOrWhiteSpace(request.Content))
        {
            return await RefuseImportAsync(identity, request, "empty_content", "none", cancellationToken)
                .ConfigureAwait(false);
        }

        if (request.Content.Length > MaxContentChars)
        {
            return await RefuseImportAsync(identity, request, "content_too_large", "none", cancellationToken)
                .ConfigureAwait(false);
        }

        var fingerprint = CashBookImportKey.Fingerprint(request.Content);
        var resourceId = Convert.ToHexString(fingerprint);

        var cashAccount = await context.Accounts
            .AsNoTracking()
            .Where(each => each.Id == request.CashAccountId)
            .Select(each => new { each.Id, each.SubType })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (cashAccount is null)
        {
            // Absent rather than forbidden, the same rule as the single-transaction endpoint.
            return await RefuseImportAsync(identity, request, "cash_account_not_found", resourceId, cancellationToken)
                .ConfigureAwait(false);
        }

        var currency = await ResolveCurrencyAsync(request.Currency, cancellationToken).ConfigureAwait(false);
        if (currency is null)
        {
            return await RefuseImportAsync(identity, request, "currency_unresolvable", resourceId, cancellationToken)
                .ConfigureAwait(false);
        }

        var parse = CashBookImportParser.Parse(request.Content);
        var accountsByCategory = await ResolveCategoriesAsync(
            request.CategoryAccountCodes, cancellationToken).ConfigureAwait(false);

        // The deterministic ids this file would produce, asked once — a re-upload finds its own
        // document rows here and never claims a second copy of them.
        var candidateIds = parse.Sides
            .Select(side => CashBookImportKey.TransactionId(fingerprint, side.LineNumber, side.Kind))
            .ToArray();

        var knownIds = (await context.CashTransactions
                .AsNoTracking()
                .Where(each => candidateIds.Contains(each.Id))
                .Select(each => each.Id)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false))
            .ToHashSet();

        var rows = new List<ImportCashBookRowResult>(
            parse.Sides.Count + parse.Errors.Count);

        foreach (var error in parse.Errors)
        {
            rows.Add(new ImportCashBookRowResult(
                error.LineNumber,
                error.Kind?.ToString().ToLowerInvariant(),
                "refused",
                null, null, null,
                [new ImportCashBookRowError(error.Code.ToString(), error.Message)]));
        }

        var posted = 0;
        var alreadyPosted = 0;
        var refused = parse.Errors.Count;

        foreach (var side in parse.Sides)
        {
            var transactionId = CashBookImportKey.TransactionId(fingerprint, side.LineNumber, side.Kind);

            var draft = CashBookImportMapper.Map(side, cashAccount.Id, currency.Value, accountsByCategory);
            if (!draft.Succeeded)
            {
                refused++;
                rows.Add(RefusedRow(side, draft.Error!.Code.ToString(), draft.Error.Message));
                continue;
            }

            var validation = CashTransactionValidator.Validate(draft.Transaction!);
            if (!validation.Succeeded)
            {
                refused++;
                rows.Add(RefusedRow(side, validation.Errors));
                continue;
            }

            var transaction = new CashTransaction
            {
                Id = transactionId,
                Kind = side.Kind,
                TransactionDate = side.TransactionDate,
                CashAccountId = cashAccount.Id,
                CounterAccountId = draft.Transaction!.CounterAccountId,
                Amount = draft.Transaction.Amount!.Value.Amount,
                Currency = draft.Transaction.Amount.Value.Currency.Code,
                Memo = draft.Transaction.Memo,
                ActorUserId = identity.UserId,
                CreatedAt = DateTimeOffset.UtcNow,
            };

            var plan = CashPosting.For(transaction, cashAccount.SubType);
            if (!plan.Succeeded)
            {
                refused++;
                rows.Add(RefusedRow(side, plan.Errors));
                continue;
            }

            // The import stamps its own source type so the corpus' entries name their provenance;
            // the source id is the deterministic document id, which is what makes the re-upload safe.
            var outcome = await poster
                .PostAsync(plan.Request! with { SourceType = CashSourceTypes.Import }, cancellationToken)
                .ConfigureAwait(false);

            if (!outcome.Succeeded)
            {
                refused++;
                rows.Add(new ImportCashBookRowResult(
                    side.LineNumber,
                    side.Kind.ToString().ToLowerInvariant(),
                    "refused",
                    null, null, null,
                    [.. outcome.Errors.Select(each =>
                        new ImportCashBookRowError(each.Code.ToString(), each.Message))]));
                continue;
            }

            if (outcome.Status == PostingOutcomeStatus.AlreadyPosted)
            {
                alreadyPosted++;
            }
            else
            {
                posted++;
            }

            transaction.PostedEntryId = outcome.EntryId;
            if (knownIds.Add(transaction.Id))
            {
                context.CashTransactions.Add(transaction);
            }

            rows.Add(new ImportCashBookRowResult(
                side.LineNumber,
                side.Kind.ToString().ToLowerInvariant(),
                outcome.Status == PostingOutcomeStatus.AlreadyPosted ? "already_posted" : "posted",
                transaction.Id,
                outcome.EntryId,
                outcome.Number,
                []));
        }

        audit.Append(new AuditRecord(
            ActorUserId: identity.UserId,
            ActorName: identity.Name,
            Action: ImportAction,
            Resource: ImportResource,
            ResourceId: resourceId,
            Outcome: AuditOutcome.Allowed,
            Detail: new
            {
                fileName = request.FileName,
                rows = parse.Sides.Count,
                posted,
                alreadyPosted,
                refused,
            },
            CorrelationId: actor.CorrelationId,
            SourceAddress: actor.SourceAddress));

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Ok(new ImportCashBookResponse(
            resourceId,
            posted,
            alreadyPosted,
            refused,
            [.. rows.OrderBy(row => row.Line)]));
    }

    /// <summary>
    /// The account id each category maps to, resolved through the company's own chart. Categories are
    /// matched trimmed and case-insensitively; a code the company does not have simply does not map,
    /// and each row that needs it is refused <c>CategoryUnmapped</c> naming the category — the refusal
    /// the importer chose over silently creating accounts.
    /// </summary>
    private async Task<IReadOnlyDictionary<string, Guid>> ResolveCategoriesAsync(
        IReadOnlyDictionary<string, string> categoryAccountCodes,
        CancellationToken cancellationToken)
    {
        var codes = categoryAccountCodes.Values
            .Select(code => code.Trim())
            .Where(code => code.Length > 0)
            .Distinct()
            .ToArray();

        var accounts = await context.Accounts
            .AsNoTracking()
            .Where(account => codes.Contains(account.Code))
            .Select(account => new { account.Id, account.Code })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var idsByCode = accounts.ToDictionary(account => account.Code, account => account.Id);

        var map = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        foreach (var (category, code) in categoryAccountCodes)
        {
            if (idsByCode.TryGetValue(code.Trim(), out var accountId))
            {
                map[category.Trim()] = accountId;
            }
        }

        return map;
    }

    private static ImportCashBookRowResult RefusedRow(CashBookImportSide side, string code, string message) =>
        new(
            side.LineNumber,
            side.Kind.ToString().ToLowerInvariant(),
            "refused",
            null, null, null,
            [new ImportCashBookRowError(code, message)]);

    private static ImportCashBookRowResult RefusedRow(
        CashBookImportSide side,
        IReadOnlyList<CashTransactionError> errors) =>
        new(
            side.LineNumber,
            side.Kind.ToString().ToLowerInvariant(),
            "refused",
            null, null, null,
            [.. errors.Select(each => new ImportCashBookRowError(each.Code.ToString(), each.Message))]);

    /// <summary>
    /// A whole-import refusal, audited as denied. The write is saved on its own because the request
    /// never reaches the processing path — a refusal that left no row would be untraceable, which is
    /// the property the audit trail exists to remove.
    /// </summary>
    private async Task<IActionResult> RefuseImportAsync(
        ActorIdentity identity,
        ImportCashBookRequest request,
        string reason,
        string resourceId,
        CancellationToken cancellationToken)
    {
        audit.Append(new AuditRecord(
            ActorUserId: identity.UserId,
            ActorName: identity.Name,
            Action: ImportAction,
            Resource: ImportResource,
            ResourceId: resourceId,
            Outcome: AuditOutcome.Denied,
            Detail: new { fileName = request.FileName, reason },
            CorrelationId: actor.CorrelationId,
            SourceAddress: actor.SourceAddress));

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return BadRequest(new { reason });
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
