namespace Ostagar.Ledger;

/// <summary>
/// The journals every company starts with.
/// </summary>
/// <remarks>
/// Six, and each one exists because a document type needs its own gapless sequence rather than its own
/// row in a settings screen. A single journal for everything would number an invoice and a payroll
/// transfer as neighbours, so a missing number would mean nothing and an owner could not say "the
/// entries between INV-000041 and INV-000043 are the ones I am asking about".
///
/// <see cref="OpeningCode"/> is the one this list exists to include. The legacy build had
/// <c>JournalType.Opening</c> in its enum and referenced it nowhere, so opening balances were posted as
/// ordinary manual journals with no marker, no separate sequence, and no way to tell the founding
/// figures of a company from a Friday correction.
/// </remarks>
public static class StandardJournals
{
    public const string GeneralCode = "GL";
    public const string SalesCode = "SALE";
    public const string PurchaseCode = "PUR";
    public const string CashCode = "CASH";
    public const string BankCode = "BANK";
    public const string OpeningCode = "OPEN";

    public static IReadOnlyList<JournalSpec> Journals { get; } =
    [
        new(GeneralCode, "General Journal", "জেনারেল জার্নাল", "GL"),
        new(SalesCode, "Sales Journal", "বিক্রয় জার্নাল", "INV"),
        new(PurchaseCode, "Purchases Journal", "ক্রয় জার্নাল", "BILL"),
        new(CashCode, "Cash Book", "নগদ বই", "CASH"),
        new(BankCode, "Bank Journal", "ব্যাংক জার্নাল", "BNK"),
        new(OpeningCode, "Opening Balance Journal", "আরম্ভিক ব্যালেন্স জার্নাল", "OPEN"),
    ];
}
