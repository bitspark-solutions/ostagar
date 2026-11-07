namespace Ostagar.Ledger;

/// <summary>
/// Gives a company the ledger it needs before any document can be posted.
/// </summary>
/// <remarks>
/// Provisioning is <b>idempotent per fiscal year</b> and the reason is operational rather than
/// theoretical: onboarding a factory means importing a closing balance sheet from whatever the
/// accountant used before, and that run gets interrupted. A first attempt that half-wrote the chart of
/// accounts and then crashed would leave a company unable to receive its balances, with no obvious way
/// to finish. Re-running the same year is therefore expected, and it must add nothing.
///
/// It does not create the tenant. That is tenancy's job and happens first; this gives an existing
/// company its books.
/// </remarks>
public interface ILedgerProvisioner
{
    /// <summary>
    /// Creates the twelve periods of one fiscal year, the base chart of accounts, and the standard
    /// journals — skipping whatever already exists.
    /// </summary>
    /// <param name="fiscalYear">The year to open, which for a July start runs into the next calendar year.</param>
    /// <param name="cancellationToken">Cancels the provisioning run; it is safe to start again.</param>
    Task ProvisionAsync(int fiscalYear, CancellationToken cancellationToken = default);

    /// <summary>Whether the given year's periods exist, so a caller can report what it actually did.</summary>
    Task<bool> IsProvisionedAsync(int fiscalYear, CancellationToken cancellationToken = default);
}
