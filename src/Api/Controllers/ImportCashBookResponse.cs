namespace Ostagar.Api.Controllers;

/// <summary>
/// The summary of one import attempt: per-row outcomes plus the totals a clerk reconciles against
/// the file.
/// </summary>
/// <param name="FileFingerprint">SHA-256 of the uploaded content, hex; the identity the idempotency
/// keys derive from and the value to quote when asking "did this file already run".</param>
/// <param name="Posted">Rows that wrote a new ledger entry.</param>
/// <param name="AlreadyPosted">Rows the ledger already had — a re-uploaded file, not new money.</param>
/// <param name="Refused">Rows that could not become a transaction, each with reasons in
/// <paramref name="Rows"/>.</param>
/// <param name="Rows">Every row-side and its outcome, in file order.</param>
public sealed record ImportCashBookResponse(
    string FileFingerprint,
    int Posted,
    int AlreadyPosted,
    int Refused,
    IReadOnlyList<ImportCashBookRowResult> Rows);
