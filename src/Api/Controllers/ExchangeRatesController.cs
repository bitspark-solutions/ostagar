using Ostagar.Core;
using Ostagar.Data;
using Ostagar.Ledger;
using Ostagar.Platform.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ostagar.Api.Controllers;

/// <summary>
/// The company's effective-dated exchange-rate history over HTTP.
/// </summary>
/// <remarks>
/// Rates are append-only facts: a recorded quote is never edited or deleted, and a wrong one is
/// superseded by a new quote with a later <c>ValidFrom</c>. This surface adds no rate rule of its own —
/// positivity, the functional-currency refusal and the same-day clash are <see cref="IExchangeRates"/>'
/// rules, reached here exactly as the posting engine reaches them, so HTTP and ledger enforce one copy.
/// </remarks>
[ApiController]
[Route("api/v1/ledger/exchange-rates")]
public class ExchangeRatesController(
    OstagarDbContext context,
    IExchangeRates exchangeRates,
    ICurrentActor actor) : ControllerBase
{
    /// <summary>
    /// The recorded rate history, newest effective date first. An optional ISO code filter narrows
    /// the answer to one currency's history.
    /// </summary>
    [HttpGet]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> List(
        [FromQuery] string? currency,
        CancellationToken cancellationToken)
    {
        CurrencyCode? parsed = null;
        if (!string.IsNullOrWhiteSpace(currency))
        {
            if (!CurrencyCode.TryFrom(currency, out var code))
            {
                return BadRequest(new { reason = "currency_unresolvable" });
            }

            parsed = code;
        }

        var rows = await context.ExchangeRates
            .AsNoTracking()
            .Where(each => parsed == null || each.Currency == parsed.Value)
            .OrderByDescending(each => each.ValidFrom)
            .ThenByDescending(each => each.RecordedAt)
            .Select(each => new ExchangeRateResponse(
                each.Id,
                each.Currency.ToString(),
                each.ValidFrom,
                each.Rate,
                each.Source,
                each.RecordedAt,
                each.RecordedByUserId))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return Ok(rows);
    }

    /// <summary>
    /// The quote in force for a currency on a date — the newest quote effective on or before it.
    /// </summary>
    /// <remarks>
    /// Answers 404 when no quote covers the date, because "never recorded a rate" and "recorded one
    /// but it starts later" are different situations a caller must distinguish, and a null body would
    /// not say which. The posting engine uses the same resolution path for foreign-currency lines.
    /// </remarks>
    [HttpGet("resolve")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Resolve(
        [FromQuery] string currency,
        [FromQuery] DateOnly date,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(currency) || !CurrencyCode.TryFrom(currency, out var code))
        {
            return BadRequest(new { reason = "currency_unresolvable" });
        }

        var quote = await exchangeRates.FindAsync(code, date, cancellationToken).ConfigureAwait(false);

        if (quote is null)
        {
            return NotFound(new { reason = "rate_not_found", currency = code.ToString(), date });
        }

        return Ok(new { currency = quote.Currency.ToString(), validFrom = quote.ValidFrom, rate = quote.Rate });
    }

    /// <summary>Appends one quote to the history. Audited as <c>ledger.rate.record</c>.</summary>
    [HttpPost]
    [Authorize(Roles = "Owner,Accounts")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Create(
        [FromBody] RecordExchangeRateRequest request,
        CancellationToken cancellationToken)
    {
        var postingActor = actor.IsAuthenticated ? actor.Current?.UserId : null;
        if (postingActor is null)
        {
            return Unauthorized(new { reason = "actor_required_to_record" });
        }

        if (!CurrencyCode.TryFrom(request.Currency, out var currency))
        {
            return BadRequest(new { reason = "currency_unresolvable" });
        }

        if (string.IsNullOrWhiteSpace(request.Source))
        {
            return BadRequest(new { reason = "source_required" });
        }

        var outcome = await exchangeRates
            .RecordAsync(
                new ExchangeRateQuote(currency, request.ValidFrom, request.Rate),
                request.Source.Trim(),
                postingActor.Value,
                cancellationToken)
            .ConfigureAwait(false);

        if (!outcome.Succeeded)
        {
            return BadRequest(new
            {
                reason = "rate_refused",
                errors = outcome.Errors.Select(each => new { code = each.Code.ToString(), message = each.Message }),
            });
        }

        return StatusCode(StatusCodes.Status201Created, new { recorded = true });
    }
}
