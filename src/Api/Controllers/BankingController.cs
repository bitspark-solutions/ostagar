using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ostagar.Core;
using Ostagar.Data;
using Ostagar.Ledger;
using Ostagar.Modules.CashBook;
using Ostagar.Platform.Security;

namespace Ostagar.Api.Controllers;

[ApiController]
[Route("api/v1/banking")]
[BankingExceptionFilter]
public sealed class BankingController(OstagarDbContext context, Banking banking, ICurrentActor actor) : ControllerBase
{
    [HttpGet("accounts")]
    [Authorize]
    public async Task<IActionResult> Accounts(CancellationToken ct) => Ok(await context.BankAccounts.AsNoTracking()
        .OrderBy(x => x.Code).Select(x => new { x.Id, x.Code, x.BankName, x.AccountNumber, x.LedgerAccountId,
            Currency = x.Currency.Value }).ToListAsync(ct));

    [HttpPost("accounts")]
    [Authorize(Roles = BankingVocabulary.WriteRoles)]
    public async Task<IActionResult> CreateAccount(CreateBankAccountRequest request, CancellationToken ct)
    {
        if (actor.Current?.UserId is not Guid userId) return Unauthorized();
        var bank = await banking.CreateAccountAsync(request.LedgerAccountId, request.Code, request.BankName,
            request.AccountNumber, CurrencyCode.From(request.Currency), userId, ct);
        return CreatedAtAction(nameof(Accounts), new { id = bank.Id }, new { bank.Id });
    }

    [HttpPost("transfers")]
    [Authorize(Roles = BankingVocabulary.WriteRoles)]
    public async Task<IActionResult> Transfer(BankTransferDraft request, CancellationToken ct)
    {
        if (actor.Current?.UserId is not Guid userId) return Unauthorized();
        var result = await banking.TransferAsync(request, userId, ct);
        return result.Succeeded ? Ok(new { result.EntryId, result.Number, Status = result.Status.ToString() })
            : BadRequest(new { errors = result.Errors.Select(x => new { code = x.Code.ToString(), x.Message }) });
    }

    [HttpGet("accounts/{id:guid}/statement")]
    [Authorize]
    public async Task<IActionResult> Statement(Guid id, [FromQuery] DateOnly asOf, CancellationToken ct)
    {
        var report = await banking.StatementAsync(id, asOf, ct);
        return report is null ? NotFound() : Ok(report);
    }

    [HttpGet("accounts/{id:guid}/reconciliations")]
    [Authorize]
    public async Task<IActionResult> Reconciliations(Guid id, CancellationToken ct)
    {
        if (!await context.BankAccounts.AnyAsync(x => x.Id == id, ct)) return NotFound();
        return Ok(await context.BankReconciliations.AsNoTracking().Where(x => x.BankAccountId == id)
            .OrderBy(x => x.To).Select(x => new { x.Id, x.From, x.To, x.OpeningBalance, x.ClosingBalance,
                x.ActorUserId, x.CreatedAt, GlLineIds = x.Matches.Select(m => m.GlLineId).ToArray() }).ToListAsync(ct));
    }

    [HttpPost("accounts/{id:guid}/reconciliations")]
    [Authorize(Roles = BankingVocabulary.WriteRoles)]
    public async Task<IActionResult> Reconcile(Guid id, BankReconciliationDraft request, CancellationToken ct)
    {
        if (actor.Current?.UserId is not Guid userId) return Unauthorized();
        var statement = await banking.ReconcileAsync(id, request, userId, ct);
        return CreatedAtAction(nameof(Reconciliations), new { id }, new { statement.Id });
    }
}
