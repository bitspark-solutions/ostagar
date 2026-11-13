using Ostagar.Ledger;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ostagar.Api.Controllers;

/// <summary>
/// Brings a tenant's books into existence: fiscal periods, the base chart of accounts, and the
/// standard journals, for one fiscal year at a time.
/// </summary>
/// <remarks>
/// Provisioning is idempotent by year — an interrupted onboarding re-runs safely and a second call
/// for the same year adds nothing. Owner-only because this is a structural act, not bookkeeping: it
/// creates the chart every later posting resolves against.
/// </remarks>
[ApiController]
[Route("api/v1/ledger/provisioning")]
public class LedgerProvisioningController(ILedgerProvisioner provisioner) : ControllerBase
{
    /// <summary>Whether the year already has all twelve periods on the books.</summary>
    [HttpGet]
    [Authorize(Roles = "Owner")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Status(
        [FromQuery] int fiscalYear,
        CancellationToken cancellationToken)
    {
        var provisioned = await provisioner.IsProvisionedAsync(fiscalYear, cancellationToken).ConfigureAwait(false);

        return Ok(new { fiscalYear, provisioned });
    }

    /// <summary>Creates the periods, base accounts and standard journals for a fiscal year.</summary>
    [HttpPost]
    [Authorize(Roles = "Owner")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(
        [FromBody] ProvisionLedgerRequest request,
        CancellationToken cancellationToken)
    {
        await provisioner.ProvisionAsync(request.FiscalYear, cancellationToken).ConfigureAwait(false);

        return StatusCode(
            StatusCodes.Status201Created,
            new { request.FiscalYear, provisioned = true });
    }
}
