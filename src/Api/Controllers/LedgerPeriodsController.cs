using Ostagar.Data;
using Ostagar.Ledger;
using Ostagar.Platform.Audit;
using Ostagar.Platform.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ostagar.Api.Controllers;

/// <summary>
/// The fiscal calendar over HTTP: listing periods and moving the three locks that close them.
/// </summary>
/// <remarks>
/// Locks are dates, not flags: a soft, hard or tax lock runs up to and including the date written on
/// the period row, and the posting engine — backed by a database trigger — refuses entries on the
/// locked side. This endpoint only moves the boundary; the refusal itself is the ledger's rule,
/// enforced in Postgres, not a courtesy re-checked here.
///
/// A lock only ever moves forward. Moving one backwards re-opens history that was already closed,
/// which is exactly the operation a lock exists to prevent — a period whose VAT return was filed must
/// not quietly accept entries again because someone adjusted a date. Re-opening, if ever needed, is a
/// deliberate act that gets its own decision, not an argument on this call.
/// </remarks>
[ApiController]
[Route("api/v1/ledger/periods")]
public class LedgerPeriodsController(
    OstagarDbContext context,
    IAuditWriter audit,
    ICurrentActor actor) : ControllerBase
{
    private const string LockAction = "ledger.period.lock";
    private const string PeriodResource = "fiscal_period";
    private static readonly string[] AllowedLevels = ["soft", "hard", "tax"];

    /// <summary>The company's fiscal calendar, oldest period first.</summary>
    [HttpGet]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var periods = await context.FiscalPeriods
            .AsNoTracking()
            .OrderBy(each => each.FiscalYear)
            .ThenBy(each => each.Number)
            .Select(each => new FiscalPeriodResponse(
                each.Id,
                each.Number,
                each.FiscalYear,
                each.Name,
                each.NameBangla,
                each.StartDate,
                each.EndDate,
                each.IsClosed,
                each.SoftLockedOn,
                each.HardLockedOn,
                each.TaxLockedOn))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return Ok(periods);
    }

    /// <summary>
    /// Moves one lock on one period to the given date.
    /// </summary>
    /// <remarks>
    /// Returns 400 rather than 404 for a date outside the period — the period exists, the request is
    /// malformed. A foreign company's period answers 404 for the usual reason: the tenant filter makes
    /// it invisible, and confirming it exists would leak across tenants.
    /// </remarks>
    [HttpPost("{id:guid}/locks")]
    [Authorize(Roles = "Owner,Accounts")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetLock(
        Guid id,
        [FromBody] SetPeriodLockRequest request,
        CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<PeriodLockLevel>(request.Level, ignoreCase: true, out var level))
        {
            return BadRequest(new { reason = "invalid_level", allowed = AllowedLevels });
        }

        var actorIdentity = actor.IsAuthenticated ? actor.Current : null;
        if (actorIdentity is null)
        {
            return Unauthorized(new { reason = "actor_required_to_lock" });
        }

        var period = await context.FiscalPeriods
            .FirstOrDefaultAsync(each => each.Id == id, cancellationToken)
            .ConfigureAwait(false);

        if (period is null)
        {
            return NotFound();
        }

        if (request.On < period.StartDate || request.On > period.EndDate)
        {
            return BadRequest(new { reason = "lock_date_outside_period", periodStart = period.StartDate, periodEnd = period.EndDate });
        }

        var existing = ExistingLock(period, level);
        if (existing.HasValue && request.On < existing.Value)
        {
            AppendLockAudit(period, level, actorIdentity, AuditOutcome.Denied);
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            return BadRequest(new
            {
                reason = "lock_moves_backwards",
                level = level.ToString().ToLowerInvariant(),
                currentLock = existing.Value,
            });
        }

        ApplyLock(period, level, request.On);
        AppendLockAudit(period, level, actorIdentity, AuditOutcome.Allowed);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Ok(new FiscalPeriodResponse(
            period.Id,
            period.Number,
            period.FiscalYear,
            period.Name,
            period.NameBangla,
            period.StartDate,
            period.EndDate,
            period.IsClosed,
            period.SoftLockedOn,
            period.HardLockedOn,
            period.TaxLockedOn));
    }

    private static DateOnly? ExistingLock(FiscalPeriod period, PeriodLockLevel level) => level switch
    {
        PeriodLockLevel.Soft => period.SoftLockedOn,
        PeriodLockLevel.Hard => period.HardLockedOn,
        PeriodLockLevel.Tax => period.TaxLockedOn,
        _ => null,
    };

    private static void ApplyLock(FiscalPeriod period, PeriodLockLevel level, DateOnly on)
    {
        switch (level)
        {
            case PeriodLockLevel.Soft:
                period.SoftLockedOn = on;
                break;
            case PeriodLockLevel.Hard:
                period.HardLockedOn = on;
                break;
            case PeriodLockLevel.Tax:
                period.TaxLockedOn = on;
                break;
        }
    }

    private void AppendLockAudit(
        FiscalPeriod period,
        PeriodLockLevel level,
        ActorIdentity actorIdentity,
        AuditOutcome outcome)
    {
        audit.Append(new AuditRecord(
            ActorUserId: actorIdentity.UserId,
            ActorName: actorIdentity.Name,
            Action: LockAction,
            Resource: PeriodResource,
            ResourceId: period.Id.ToString("D"),
            Outcome: outcome,
            CorrelationId: actor.CorrelationId,
            SourceAddress: actor.SourceAddress,
            Detail: new
            {
                level = level.ToString().ToLowerInvariant(),
                fiscalYear = period.FiscalYear,
                periodNumber = period.Number,
            }));
    }
}
