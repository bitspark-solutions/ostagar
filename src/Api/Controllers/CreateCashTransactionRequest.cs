namespace Ostagar.Api.Controllers;

/// <summary>
/// What the cash book page submits for one movement of money.
/// </summary>
public sealed record CreateCashTransactionRequest
{
    /// <summary><c>receipt</c> or <c>payment</c>, case-insensitive. A string rather than the enum so a
    /// mistyped value is a readable refusal instead of a model-binding error in framework English.</summary>
    public required string Kind { get; init; }

    public required DateOnly TransactionDate { get; init; }

    public required Guid CashAccountId { get; init; }

    public required Guid CounterAccountId { get; init; }

    public required decimal Amount { get; init; }

    /// <summary>
    /// ISO code of the currency the money moved in. Null means the company's functional currency.
    /// Foreign amounts are honoured: the ledger resolves the effective rate rather than pretending
    /// every note is a taka note.
    /// </summary>
    public string? Currency { get; init; }

    public Guid? PartnerId { get; init; }

    public string? Memo { get; init; }
}
