using Ostagar.Core;

namespace Ostagar.Modules.Partners;

/// <summary>
/// A bank account belonging to the partner — the only channel through which this product will settle money.
/// </summary>
/// <remarks>
/// <para>
/// A partner with no bank account has no payment channel, and <see cref="Partner.RequirePaymentChannel"/>
/// refuses the payment rather than defaulting to cash, to the partner's "usual" account, or to the legacy
/// behaviour of posting a receipt against a bank account the clerk picked from a dropdown. A garment factory
/// pays lakhs of taka on a supplier's word; the point of a payment channel record is that the word is
/// written down and can be read back off the bank statement.
/// </para>
///
/// <para>
/// <b>The account name is not forced to equal the legal name.</b> Bangladeshi trading houses routinely hold
/// an account styled after the trade name, or after the proprietor for a sole concern, and a validator that
/// demanded a match would reject real suppliers and push users into typing a false account name to get
/// through a form. What is required is that a name is present and is not a placeholder: an empty account name
/// tells a payer nothing about whose account they are about to credit.
/// </para>
///
/// <para>
/// Accounts are archived, never deleted. A bill paid from an account the supplier has since closed still
/// needs to show which account the money went to, and the ledger line that recorded it references this row.
/// </para>
/// </remarks>
public sealed record PartnerBankAccount
{
    public PartnerBankAccount(
        string bankName,
        string accountName,
        string accountNumber,
        Currency currency,
        string? branch = null,
        bool isPreferred = false)
    {
        BankName = RequireText(bankName, "Bank name");
        AccountName = RequireText(accountName, "Account name");
        Branch = string.IsNullOrWhiteSpace(branch) ? null : RequireText(branch, "Branch");
        AccountNumber = new PartnerIdentifier(PartnerIdentifierKind.BankAccountNumber, accountNumber).Value;
        Currency = currency;
        IsPreferred = isPreferred;
    }

    public string BankName { get; }

    public string AccountName { get; }

    public string? Branch { get; }

    /// <summary>The account number, normalised; also a suggestive identifier for duplicate detection.</summary>
    public string AccountNumber { get; }

    /// <summary>
    /// The currency the account settles in. A BDT account cannot receive a USD export remittance and the
    /// payment path needs to know that before it offers the account, not after the wire bounces.
    /// </summary>
    public Currency Currency { get; }

    /// <summary>
    /// Whether a payment must be made to this account when several exist. At most one per partner, and changed
    /// only by <see cref="Partner.DesignatePaymentChannel"/>, so a channel is never chosen by list order.
    /// </summary>
    public bool IsPreferred { get; internal set; }

    /// <summary>
    /// False for every account until archived. An archived account is excluded from payment selection but
    /// stays readable, because deleting a paid-against account would break the history that references it.
    /// </summary>
    public bool IsArchived { get; private set; }

    /// <summary>The identifier this account contributes to duplicate matching.</summary>
    public PartnerIdentifier AsIdentifier() => new(PartnerIdentifierKind.BankAccountNumber, AccountNumber);

    /// <summary>Retires the account from payment selection without removing it.</summary>
    public void Archive() => IsArchived = true;

    /// <summary>Brings a retired account back, e.g. when a supplier reopens the same facility.</summary>
    public void Reinstate() => IsArchived = false;

    private static string RequireText(string value, string fieldName)
    {
        var normalised = PartnerTextRules.Normalise(value, fieldName, PartnerTextRules.MaxNameLength);

        if (normalised.Length == 0)
        {
            throw new ArgumentException($"{fieldName} is required to record a bank account.", nameof(value));
        }

        if (PartnerTextRules.IsPlaceholder(normalised))
        {
            throw new ArgumentException(
                $"'{normalised}' is a placeholder, not a {fieldName}.",
                nameof(value));
        }

        return normalised;
    }
}
