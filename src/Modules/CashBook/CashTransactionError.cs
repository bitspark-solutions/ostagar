namespace Ostagar.Modules.CashBook;

/// <summary>
/// One refusal, in a form a caller can show or translate.
/// </summary>
/// <param name="Code">The machine-readable reason.</param>
/// <param name="Message">The human-readable reason. English here; the API returns the code alongside so
/// a client can render Bangla from its own string table.</param>
public sealed record CashTransactionError(CashTransactionErrorCode Code, string Message);
