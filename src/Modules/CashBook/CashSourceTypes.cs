namespace Ostagar.Modules.CashBook;

/// <summary>
/// The <c>SourceType</c> strings a cash posting stamps on its ledger request.
/// </summary>
/// <remarks>
/// Constants rather than inline literals because the value is half of the idempotency key
/// (invariant I5: <c>(tenant, source_type, source_id, iteration)</c>). A typo'd literal does not fail
/// at compile time — it fails as a duplicate posting a retry was supposed to prevent.
/// </remarks>
public static class CashSourceTypes
{
    public const string Receipt = "cash.receipt";

    public const string Payment = "cash.payment";

    /// <summary>
    /// Stamped on postings a CSV import produced, in place of the per-kind types. An imported row is a
    /// receipt or payment as its kind says, but its provenance is the file: the P3 tie-out needs to
    /// name the entries the corpus created, and a retry of the upload resolves them by the same
    /// <c>(source_type, source_id, iteration)</c> key — the source id being the deterministic
    /// <see cref="CashBookImportKey.TransactionId"/>, not a fresh guid.
    /// </summary>
    public const string Import = "cash.import";

    public static string Of(CashTransactionKind kind) => kind switch
    {
        CashTransactionKind.Receipt => Receipt,
        CashTransactionKind.Payment => Payment,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown cash transaction kind."),
    };
}
