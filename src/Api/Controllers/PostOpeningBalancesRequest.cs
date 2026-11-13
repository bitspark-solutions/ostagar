namespace Ostagar.Api.Controllers;

/// <summary>
/// Payload for turning a company's paper balance sheet into its founding ledger entry.
/// </summary>
/// <param name="FiscalYear">The year whose first period receives the opening entry.</param>
/// <param name="Balances">One line per account carrying a balance into the books. The residual — the
/// amount a real balance sheet fails to tie by — posts to Owners Capital, so the assumption is
/// written down rather than refused or hidden.</param>
public sealed record PostOpeningBalancesRequest(
    int FiscalYear,
    IReadOnlyList<OpeningBalanceLineRequest> Balances);
