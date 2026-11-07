namespace Ostagar.Ledger;

/// <summary>
/// The five statement classes every account belongs to.
/// </summary>
/// <remarks>
/// This is stored as a short code rather than a CLR enum ordinal so that adding a member cannot
/// renumber existing rows — the legacy tree persisted enums as integers, and its model later asked
/// for them as strings, which is how <c>Status</c> and <c>JournalType</c> ended up with the schema and
/// the entity describing different tables.
/// </remarks>
public enum AccountType
{
    /// <summary>Resources the company controls. Normal balance: debit.</summary>
    Asset,

    /// <summary>Obligations owed to others. Normal balance: credit.</summary>
    Liability,

    /// <summary>Owners' residual interest. Normal balance: credit.</summary>
    Equity,

    /// <summary>Earnings from operations and other sources. Normal balance: credit.</summary>
    Income,

    /// <summary>Costs incurred. Normal balance: debit.</summary>
    Expense,
}
