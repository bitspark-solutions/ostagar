namespace Ostagar.Api.Controllers;

/// <summary>
/// Payload for provisioning a tenant's ledger for one fiscal year.
/// </summary>
/// <param name="FiscalYear">The year to provision, e.g. 2026 for the year starting July 2026 on a
/// July-start calendar. Provisioning is idempotent per year: a repeated call adds nothing that is
/// already there.</param>
public sealed record ProvisionLedgerRequest(int FiscalYear);
