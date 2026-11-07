namespace Ostagar.Api.Controllers;

/// <summary>
/// One line of a journal being written through the API.
/// </summary>
/// <param name="AccountId">A postable account belonging to the caller's company.</param>
/// <param name="Debit">Amount on the debit side. Mutually exclusive with <paramref name="Credit"/>.</param>
/// <param name="Credit">Amount on the credit side. Mutually exclusive with <paramref name="Debit"/>.</param>
/// <param name="Memo">Line note carried into the ledger row.</param>
/// <remarks>
/// Both sides are optional and the mutual exclusion is decided by the ledger's own rules rather than
/// restated here. Two definitions of "a valid line" is how the legacy build ended up with a validator
/// that accepted ±0.01 imbalance beside a database that had no constraint at all.
/// </remarks>
public sealed record JournalLineRequest(
    Guid AccountId,
    decimal? Debit,
    decimal? Credit,
    string? Memo);
