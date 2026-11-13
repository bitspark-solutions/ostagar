using Ostagar.Ledger;
using Ostagar.Platform.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ostagar.Api.Controllers;

/// <summary>
/// The founding entry: the balances a company brings into its books on day one.
/// </summary>
/// <remarks>
/// One call produces one <c>OPEN</c>-journal entry at the start of the fiscal year's first period,
/// signed on each account's normal side, with the residual posted to Owners Capital so a balance
/// sheet that does not tie records the assumption instead of being refused. Idempotent through the
/// ledger's source key — a retry, and a second call, resolve to the same opening entry rather than
/// doubling the books. Corrections afterwards are reversals plus a repost, not edits.
/// </remarks>
[ApiController]
[Route("api/v1/ledger/opening-balances")]
public class OpeningBalancesController(
    IOpeningBalances openingBalances,
    ICurrentActor actor) : ControllerBase
{
    /// <summary>Posts the opening balance sheet as one balanced ledger entry.</summary>
    /// <remarks>
    /// Returns 201 with the entry reference when the books open. A refusal carries every problem at
    /// once — unknown codes listed together, not discovered one per save.
    /// </remarks>
    [HttpPost]
    [Authorize(Roles = "Owner,Accounts")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Create(
        [FromBody] PostOpeningBalancesRequest request,
        CancellationToken cancellationToken)
    {
        var postingActor = actor.IsAuthenticated ? actor.Current?.UserId : null;
        if (postingActor is null)
        {
            return Unauthorized(new { reason = "actor_required_to_post" });
        }

        var balances = (request.Balances ?? [])
            .Select(each => new OpeningBalance(each.AccountCode?.Trim() ?? string.Empty, each.Amount))
            .ToList();

        var outcome = await openingBalances
            .PostAsync(request.FiscalYear, balances, postingActor.Value, cancellationToken)
            .ConfigureAwait(false);

        if (!outcome.Succeeded)
        {
            return BadRequest(new
            {
                reason = "opening_balances_refused",
                errors = outcome.Errors.Select(each => new { code = each.Code.ToString(), message = each.Message }),
            });
        }

        return StatusCode(
            StatusCodes.Status201Created,
            new { entryId = outcome.EntryId, number = outcome.Number });
    }
}
