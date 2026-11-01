namespace Garments.Modules.CashBook;

/// <summary>
/// The outcome of validating or planning a cash transaction: either the thing asked for, or every
/// reason it was refused at once.
/// </summary>
/// <remarks>
/// All errors returned together, because a clerk keying a day of receipts should not have to submit
/// once per mistake to learn about all of them.
/// </remarks>
public sealed record CashTransactionDecision
{
    public IReadOnlyList<CashTransactionError> Errors { get; init; } = [];

    public bool Succeeded => Errors.Count == 0;

    public static CashTransactionDecision Ok() => new();

    public static CashTransactionDecision Refused(params CashTransactionError[] errors) =>
        new() { Errors = errors };
}
