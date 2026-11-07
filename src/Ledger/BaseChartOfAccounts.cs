namespace Ostagar.Ledger;

/// <summary>
/// The accounts every company starts with, and the hierarchy they form.
/// </summary>
/// <remarks>
/// <para>
/// Deliberately small. This is the set Tier A and Tier B need to post a first entry and read a trial
/// balance, not a full national chart of accounts — a template with 200 accounts is how an SMB ERP's
/// first-run screen becomes a project, and a factory that does not use payroll will not find the
/// payroll expense account in the way.
/// </para>
///
/// <para>
/// <b>Codes are the tenant's once provisioned.</b> Editing this list changes companies created after
/// the change and leaves existing ones alone, which is the correct asymmetry: a live ledger cannot
/// have its account codes rewritten under it. Anyone who needs a different chart adds accounts through
/// the company's own settings, not here.
/// </para>
///
/// <para>
/// <b>The Bangla names need a native review before the first pilot.</b> They are written here to make
/// law L17 true from the start — a chart of accounts that exists only in English is not a Bangla-first
/// product — but a bookkeeping term in Bangla has conventional wording a non-native speaker will get
/// subtly wrong, and a subtly wrong account name on an owner's screen is worse than an English one they
/// can read. Tracked for P9, where the reference-selling assets are written in Bangla anyway.
/// </para>
/// </remarks>
public static class BaseChartOfAccounts
{
    /// <summary>Where the opening-balance difference is parked, and what the owner's own money is called.</summary>
    public const string OwnersCapitalCode = "3010";

    /// <summary>Absorbs the residual when rounding a document total differs from rounding its lines.</summary>
    public const string RoundingCode = "7020";

    /// <summary>Ordered parents first, so a child can always resolve its path from a spec already seen.</summary>
    public static IReadOnlyList<AccountSpec> Accounts { get; } =
    [
        .. Groups(),
        .. Leaves(),
    ];

    private static IEnumerable<AccountSpec> Groups() =>
    [
        new("100", "Current Assets", "চলমান পরিসম্পৎ", AccountType.Asset, AccountSubType.Cash, null, false),
        new("110", "Non-current Assets", "স্থায়ী পরিসম্পৎ", AccountType.Asset, AccountSubType.FixedAsset, null, false),
        new("200", "Current Liabilities", "চলমান দায়", AccountType.Liability, AccountSubType.Payable, null, false),
        new("210", "Non-current Liabilities", "স্থায়ী দায়", AccountType.Liability, AccountSubType.Loan, null, false),
        new("300", "Equity", "মালিকানা স্বত্ব", AccountType.Equity, AccountSubType.OwnersCapital, null, false),
        new("400", "Income", "আয়", AccountType.Income, AccountSubType.SalesRevenue, null, false),
        new("500", "Cost of Goods Sold", "বিক্রয়কৃত পণ্যের মূল্য", AccountType.Expense, AccountSubType.CostOfGoodsSold, null, false),
        new("600", "Operating Expenses", "পরিচালন ব্যয়", AccountType.Expense, AccountSubType.OperatingExpense, null, false),
        new("700", "Finance and Other Expenses", "আর্থিক ও অন্যান্য ব্যয়", AccountType.Expense, AccountSubType.FinanceCost, null, false),
    ];

    private static IEnumerable<AccountSpec> Leaves() =>
    [
        new("1010", "Cash on Hand", "নগদ", AccountType.Asset, AccountSubType.Cash, "100", true),
        new("1020", "Bank Account", "ব্যাংক হিসাব", AccountType.Asset, AccountSubType.Bank, "100", true),
        new("1030", "Accounts Receivable", "পাওনা", AccountType.Asset, AccountSubType.Receivable, "100", true),
        new("1040", "Inventory", "পণ্য মজুদ", AccountType.Asset, AccountSubType.Inventory, "100", true),
        new("1050", "Advances to Suppliers", "সরবরাহকারীকে অগ্রিম", AccountType.Asset, AccountSubType.Prepayment, "100", true),
        new("1060", "VAT Receivable", "ভ্যাট পাওনা", AccountType.Asset, AccountSubType.Prepayment, "100", true),
        new("1110", "Plant and Machinery", "যন্ত্রপাতি", AccountType.Asset, AccountSubType.FixedAsset, "110", true),

        new("2010", "Accounts Payable", "দেনা", AccountType.Liability, AccountSubType.Payable, "200", true),
        new("2020", "VAT Payable", "ভ্যাট পরিশোধযোগ্য", AccountType.Liability, AccountSubType.TaxPayable, "200", true),
        new("2030", "Salary Payable", "বেতন পরিশোধযোগ্য", AccountType.Liability, AccountSubType.Accrual, "200", true),
        new("2040", "Short-term Loans", "স্বল্পমেয়াদি ঋণ", AccountType.Liability, AccountSubType.Loan, "200", true),
        new("2110", "Long-term Loans", "দীর্ঘমেয়াদি ঋণ", AccountType.Liability, AccountSubType.Loan, "210", true),

        new("3010", "Owners Capital", "মালিকানা মূলধন", AccountType.Equity, AccountSubType.OwnersCapital, "300", true),
        new("3020", "Retained Earnings", "সঞ্চিত আয়", AccountType.Equity, AccountSubType.RetainedEarnings, "300", true),

        new("4010", "Local Sales", "অভ্যন্তরীণ বিক্রয়", AccountType.Income, AccountSubType.SalesRevenue, "400", true),
        new("4020", "Export Sales", "রপ্তানি বিক্রয়", AccountType.Income, AccountSubType.SalesRevenue, "400", true),
        new("4030", "Service Income", "সেবা আয়", AccountType.Income, AccountSubType.ServiceRevenue, "400", true),

        new("5010", "Cost of Goods Sold", "বিক্রয়কৃত পণ্যের মূল্য", AccountType.Expense, AccountSubType.CostOfGoodsSold, "500", true),

        new("6010", "Salaries and Wages", "বেতন ও মজুরি", AccountType.Expense, AccountSubType.PayrollExpense, "600", true),
        new("6020", "Rent", "ভাড়া", AccountType.Expense, AccountSubType.RentExpense, "600", true),
        new("6030", "Utilities", "ইউটিলিটি বিল", AccountType.Expense, AccountSubType.UtilityExpense, "600", true),
        new("6040", "Other Operating Expenses", "অন্যান্য পরিচালন ব্যয়", AccountType.Expense, AccountSubType.OperatingExpense, "600", true),

        new("7010", "Bank Charges and Interest", "ব্যাংক চার্জ ও সুদ", AccountType.Expense, AccountSubType.FinanceCost, "700", true),
        new("7020", "Rounding Difference", "গোলাই পার্থক্য", AccountType.Expense, AccountSubType.OperatingExpense, "700", true),
    ];

    /// <summary>
    /// Resolves one spec's ltree path from its parent's code, so a hierarchy written as parents and
    /// children can be stored as the materialised path the schema indexes.
    /// </summary>
    /// <remarks>
    /// Codes, not names, because the path is what ancestor queries walk and a company that renames an
    /// account must not move its subtree. The codes in this template are digits, which ltree labels
    /// accept; a tenant-defined account with a symbol in its code cannot become a path segment, which is
    /// a real limit and belongs in the account-creation validation rather than here.
    /// </remarks>
    public static string PathOf(AccountSpec spec, IReadOnlyDictionary<string, ChartOfAccount> byCode)
    {
        if (spec.ParentCode is null)
        {
            return spec.Code;
        }

        var parent = byCode[spec.ParentCode];

        return $"{parent.Path}.{spec.Code}";
    }
}
