using Ostagar.Core;
using Ostagar.Ledger;
using Ostagar.Platform.Audit;
using Ostagar.Platform.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace Ostagar.Data;

/// <summary>
/// Reads and appends the company's effective-dated rate history.
/// </summary>
/// <remarks>
/// Recording goes through the same audit writer as a posting and stages into the caller's unit of work,
/// because a rate that arrives with no record of who typed it is exactly the artefact an adjustment gets
/// argued over at year end.
/// </remarks>
public sealed class ExchangeRates(
    OstagarDbContext context,
    ITenantContext tenantContext,
    IAuditWriter audit) : IExchangeRates
{
    public async Task<ExchangeRateQuote?> FindAsync(
        CurrencyCode currency,
        DateOnly postingDate,
        CancellationToken cancellationToken = default)
    {
        RequireTenant();

        // The newest row on or before the date, which is what effective dating means. Descending on the
        // date is the index this read was shaped around; a scan of a company's whole rate history on
        // every foreign-currency line is what it would otherwise cost.
        var row = await context.ExchangeRates
            .Where(each => each.Currency == currency && each.ValidFrom <= postingDate)
            .OrderByDescending(each => each.ValidFrom)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return row is null ? null : new ExchangeRateQuote(row.Currency, row.ValidFrom, row.Rate);
    }

    public async Task<PostingOutcome> RecordAsync(
        ExchangeRateQuote quote,
        string source,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        var tenantId = RequireTenant();

        var functional = await FunctionalCurrencyAsync(cancellationToken).ConfigureAwait(false);

        if (quote.Currency == functional)
        {
            return new PostingOutcome(PostingOutcomeStatus.Rejected, [new PostingError(
                PostingErrorCode.InvalidExchangeRate,
                $"{functional} is this company's own currency, so its rate is 1 by definition. A row for "
                + "it would let a rate that is not 1 price a line the ledger already holds in that currency.")]);
        }

        if (quote.Rate <= 0m)
        {
            return new PostingOutcome(PostingOutcomeStatus.Rejected, [new PostingError(
                PostingErrorCode.InvalidExchangeRate,
                "An exchange rate must be positive so it can be inverted and explained.")]);
        }

        var clash = await context.ExchangeRates
            .FirstOrDefaultAsync(
                each => each.Currency == quote.Currency && each.ValidFrom == quote.ValidFrom,
                cancellationToken)
            .ConfigureAwait(false);

        if (clash is not null)
        {
            // Refused in the readable place, though the unique index also refuses it: a rate already used
            // by a posted line cannot be restated underneath it, so the correction is a row with a
            // different effective date and nothing else.
            return new PostingOutcome(PostingOutcomeStatus.Rejected, [new PostingError(
                PostingErrorCode.InvalidExchangeRate,
                $"{quote.Currency} already has a rate effective on {quote.ValidFrom:yyyy-MM-dd} "
                + $"({clash.Rate:0.##########}). Rates are append-only: record the replacement under a "
                + "new effective date rather than restating a number the ledger may have posted against.")]);
        }

        var recorded = new ExchangeRate
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Currency = quote.Currency,
            ValidFrom = quote.ValidFrom,
            Rate = quote.Rate,
            Source = source,
            RecordedAt = DateTimeOffset.UtcNow,
            RecordedByUserId = actorUserId,
        };

        context.ExchangeRates.Add(recorded);

        audit.Append(new AuditRecord(
            ActorUserId: actorUserId,
            ActorName: string.Empty,
            Action: "ledger.rate.record",
            Resource: "exchange_rate",
            ResourceId: recorded.Id.ToString("D"),
            Outcome: AuditOutcome.Allowed,
            Detail: new
            {
                currency = quote.Currency.ToString(),
                validFrom = quote.ValidFrom.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
                rate = quote.Rate,
                source,
            }));

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new PostingOutcome(PostingOutcomeStatus.Posted, [], recorded.Id, $"{quote.Currency}");
    }

    private async Task<CurrencyCode> FunctionalCurrencyAsync(CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.TenantId;

        var code = await context.Tenants
            .AsNoTracking()
            .Where(each => each.Id == tenantId)
            .Select(each => each.FunctionalCurrencyCode)
            .FirstAsync(cancellationToken)
            .ConfigureAwait(false);

        return CurrencyCode.From(code);
    }

    private TenantId RequireTenant()
    {
        var tenantId = tenantContext.TenantId;

        if (tenantId.IsEmpty)
        {
            throw new InvalidOperationException(
                "Exchange rates are a company's own history. Resolving one without a tenant would hand "
                + "one company another company's price, and the difference only shows up in an amount.");
        }

        return tenantId;
    }
}
