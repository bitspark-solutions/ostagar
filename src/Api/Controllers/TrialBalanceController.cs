using Ostagar.Data;
using Ostagar.Ledger;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ostagar.Api.Controllers;

/// <summary>
/// The trial balance: a projection over the posted general ledger, not a table.
/// </summary>
/// <remarks>
/// <para>
/// The report is computed on read from <c>gl_lines</c> joined to posted <c>gl_entries</c> — the same
/// rows the balance triggers guard — so it cannot disagree with the ledger by definition. It is the
/// honest version of the legacy trial balance, which filtered on a status the posting path never set
/// and therefore showed broken entries while hiding correct ones.
/// </para>
/// <para>
/// Scope is a period, an as-of date, or both. Asking for neither is refused rather than silently
/// answered with "all of history", because an unbounded sum over a growing ledger has no honest limit
/// and every caller who means "the year so far" should say so.
/// </para>
/// </remarks>
[ApiController]
[Route("api/v1/ledger/trial-balance")]
public class TrialBalanceController(OstagarDbContext context) : ControllerBase
{
    /// <summary>
    /// Accounts' debit and credit columns for posted entries in scope, with the tie-out totals.
    /// </summary>
    /// <remarks>
    /// <paramref name="periodId"/> restricts to one period's movements; <paramref name="asOf"/>
    /// restricts to postings on or before a date. Both may be given together (period movements up to
    /// a date inside it); neither may be omitted.
    /// </remarks>
    /// <param name="periodId">Optional period scope.</param>
    /// <param name="asOf">Optional date ceiling.</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    [HttpGet]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Read(
        [FromQuery] Guid? periodId,
        [FromQuery] DateOnly? asOf,
        CancellationToken cancellationToken)
    {
        if (periodId is null && asOf is null)
        {
            return BadRequest(new { reason = "filter_required" });
        }

        var movements = context.GlLines
            .AsNoTracking()
            .Join(
                context.GlEntries.AsNoTracking(),
                line => line.EntryId,
                entry => entry.Id,
                (line, entry) => new { line.AccountId, line.Debit, line.Credit, entry.Status, entry.PeriodId, entry.PostingDate })
            .Where(each => each.Status == GlEntryStatus.Posted);

        if (periodId is { } period)
        {
            movements = movements.Where(each => each.PeriodId == period);
        }

        if (asOf is { } date)
        {
            movements = movements.Where(each => each.PostingDate <= date);
        }

        var totals = await movements
            .GroupBy(each => each.AccountId)
            .Select(each => new
            {
                AccountId = each.Key,
                TotalDebit = each.Sum(line => line.Debit ?? 0m),
                TotalCredit = each.Sum(line => line.Credit ?? 0m),
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var accountIds = totals.Select(each => each.AccountId).ToArray();

        var accounts = await context.Accounts
            .AsNoTracking()
            .Where(each => accountIds.Contains(each.Id))
            .Select(each => new { each.Id, each.Code, each.Name, each.NameBangla, each.Type })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var rows = totals
            .Join(
                accounts,
                total => total.AccountId,
                account => account.Id,
                (total, account) => new TrialBalanceRowResponse(
                    account.Id,
                    account.Code,
                    account.Name,
                    account.NameBangla,
                    account.Type.ToString(),
                    total.TotalDebit,
                    total.TotalCredit,
                    NormalBalanceOf.Balance(total.TotalDebit, total.TotalCredit, account.Type)))
            .OrderBy(each => each.Code, StringComparer.Ordinal)
            .ToList();

        var totalDebit = rows.Sum(each => each.TotalDebit);
        var totalCredit = rows.Sum(each => each.TotalCredit);

        return Ok(new TrialBalanceResponse(rows, totalDebit, totalCredit, totalDebit == totalCredit));
    }
}
