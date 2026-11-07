using Ostagar.Ledger;

namespace Ostagar.Modules.Partners;

/// <summary>
/// The ledger account a partner's balance lives on, and — the part that matters — which side of the ledger
/// it is on.
/// </summary>
/// <remarks>
/// <para>
/// A partner that is both customer and vendor has two accounts and they must never be conflated. The
/// receivable is an asset that grows on a debit; the payable is a liability that grows on a credit. The
/// legacy tree kept one "Account" field on its party record, so a dual-role trading house's bill and its
/// invoice landed on the same account and netted silently — the owner's "what do I owe" and "what am I owed"
/// became one number, and both were wrong.
/// </para>
///
/// <para>
/// This type therefore cannot be constructed for anything but the two trade subtypes. A caller that wants to
/// point a partner at an expense or a bank account has to be refused here, at the point where the mistake
/// would otherwise become a posted line. The declared subtype is checked against the chart at write time by
/// the ledger itself; what this guarantees is that a partner's trade account is a receivable or a payable and
/// nothing else.
/// </para>
/// </remarks>
public sealed record LedgerAccountReference
{
    private LedgerAccountReference(string code, AccountSubType subType)
    {
        Code = code;
        SubType = subType;
    }

    /// <summary>The chart-of-accounts code, as the tenant's chart spells it.</summary>
    public string Code { get; }

    /// <summary><see cref="AccountSubType.Receivable"/> or <see cref="AccountSubType.Payable"/>, never another.</summary>
    public AccountSubType SubType { get; }

    /// <summary>
    /// The statement class implied by the subtype. Derived rather than stored: a reference that keeps its own
    /// copy of the account type can disagree with the chart it points at, and the disagreement is invisible
    /// until a balance sheet is wrong.
    /// </summary>
    public AccountType AccountType => SubType == AccountSubType.Receivable ? AccountType.Asset : AccountType.Liability;

    /// <summary>Which side this partner's balance normally sits on, read the same way the ledger reads it.</summary>
    public NormalBalance Side => NormalBalanceOf.For(AccountType);

    /// <summary>The account a customer's outstanding invoices sit on.</summary>
    public static LedgerAccountReference ForReceivable(string code) => new(ValidateCode(code, nameof(code)), AccountSubType.Receivable);

    /// <summary>The account a vendor's unpaid bills sit on.</summary>
    public static LedgerAccountReference ForPayable(string code) => new(ValidateCode(code, nameof(code)), AccountSubType.Payable);

    public override string ToString() => $"{Code} ({SubType})";

    private static string ValidateCode(string code, string paramName)
    {
        ArgumentNullException.ThrowIfNull(code, paramName);

        var trimmed = code.Trim();

        if (trimmed.Length == 0)
        {
            throw new ArgumentException(
                "A partner needs a ledger account on each side it trades. An unassigned account is refused here rather than defaulted downstream.",
                paramName);
        }

        if (trimmed.Length > 32 || trimmed.Any(char.IsControl) || trimmed.Any(char.IsWhiteSpace))
        {
            throw new ArgumentException(
                $"'{code}' is not shaped like an account code.",
                paramName);
        }

        return trimmed;
    }
}
