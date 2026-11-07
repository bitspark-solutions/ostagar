using Ostagar.Core;

namespace Ostagar.Modules.CashBook;

/// <summary>
/// What the daily cash book page submits, before a document exists.
/// </summary>
/// <remarks>
/// One transaction is exactly two accounts: the cash (or bank) account the money moved through, and the
/// counter account that says what the movement was for. The the client book this replaces splits a day
/// into a "categories" grid — the counter account is what a category always was, and naming it an
/// account here is what makes every row land in the ledger instead of in a report that later disagrees
/// with it.
/// </remarks>
public sealed record NewCashTransaction
{
    public required CashTransactionKind Kind { get; init; }

    public required DateOnly TransactionDate { get; init; }

    /// <summary>The company's own cash or bank account the money moved through.</summary>
    public required Guid CashAccountId { get; init; }

    /// <summary>The account the movement is for — a sales, rent, purchase or partner account.</summary>
    public required Guid CounterAccountId { get; init; }

    /// <summary>
    /// The amount as counted, in its own currency. <see cref="Money"/> rather than a bare decimal so a
    /// BDT receipt and a USD payment can never be added into the same total unmarked.
    /// </summary>
    public Money? Amount { get; init; }

    /// <summary>The partner this movement settles with, when one applies. Optional in v1.</summary>
    public Guid? PartnerId { get; init; }

    public string Memo { get; init; } = string.Empty;
}
