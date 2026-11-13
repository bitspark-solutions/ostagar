namespace Ostagar.Api.Controllers;

/// <summary>
/// What happened to one row-side of an uploaded file: posted, found already posted, or refused with
/// reasons.
/// </summary>
/// <param name="Line">One-based line number in the file.</param>
/// <param name="Kind"><c>receipt</c> or <c>payment</c> — which half of the two-sided row this was.</param>
/// <param name="Outcome"><c>posted</c>, <c>already_posted</c> or <c>refused</c>.</param>
/// <param name="TransactionId">The deterministic document id; present on posted and already-posted
/// rows.</param>
/// <param name="EntryId">The ledger entry written or found.</param>
/// <param name="Number">The journal number, for showing the clerk what was filed.</param>
/// <param name="Errors">Every reason the row was refused, when it was.</param>
public sealed record ImportCashBookRowResult(
    int Line,
    string? Kind,
    string Outcome,
    Guid? TransactionId,
    Guid? EntryId,
    string? Number,
    IReadOnlyList<ImportCashBookRowError> Errors);
