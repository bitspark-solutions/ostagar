namespace Garments.Api.Controllers;

/// <summary>
/// One cash book row as the API returns it, with the entry links resolved.
/// </summary>
public sealed record CashTransactionResponse(
    Guid Id,
    string Kind,
    string Status,
    DateOnly TransactionDate,
    Guid CashAccountId,
    string CashAccountCode,
    Guid CounterAccountId,
    string CounterAccountCode,
    decimal Amount,
    string Currency,
    Guid? PartnerId,
    string Memo,
    Guid? PostedEntryId,
    string? PostedEntryNumber,
    Guid? ReversedByEntryId);
