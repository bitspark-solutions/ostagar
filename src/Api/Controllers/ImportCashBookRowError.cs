namespace Ostagar.Api.Controllers;

/// <summary>
/// One reason a row-side was refused, carrying the cash book's or the ledger's machine-readable code.
/// </summary>
public sealed record ImportCashBookRowError(string Code, string Message);
