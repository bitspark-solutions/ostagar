namespace Ostagar.Modules.CashBook;

/// <summary>
/// What <see cref="CashBookImportMapper.Map"/> made of one parsed side: a draft ready for
/// <see cref="CashTransactionValidator"/>, or the reason none could be built.
/// </summary>
public sealed record CashBookImportDraft
{
    public NewCashTransaction? Transaction { get; init; }

    public CashBookImportError? Error { get; init; }

    public bool Succeeded => Transaction is not null;

    public static CashBookImportDraft Ready(NewCashTransaction transaction) =>
        new() { Transaction = transaction };

    public static CashBookImportDraft Refused(CashBookImportError error) => new() { Error = error };
}
