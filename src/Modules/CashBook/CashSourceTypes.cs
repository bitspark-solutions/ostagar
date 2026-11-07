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

    public static string Of(CashTransactionKind kind) => kind switch
    {
        CashTransactionKind.Receipt => Receipt,
        CashTransactionKind.Payment => Payment,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown cash transaction kind."),
    };
}
