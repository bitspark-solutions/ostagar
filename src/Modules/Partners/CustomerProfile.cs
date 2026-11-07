using Ostagar.Core;

namespace Ostagar.Modules.Partners;

/// <summary>
/// The customer capability on a partner: the receivable account, the credit limit and the default payment
/// term. Present only when we actually sell to this party.
/// </summary>
/// <remarks>
/// A capability rather than a record type, because a factory's buyer list and its supplier list are not two
/// populations — they overlap, and the overlap is where the money is. Splitting the two into separate
/// entities is what makes a trading house that both buys and supplies look like two businesses in an ageing
/// report.
///
/// <see cref="CreditLimit"/> is deliberately a <see cref="Money"/> and deliberately nullable, and the two
/// choices work against each other on purpose. As money it cannot be added to an exposure in another
/// currency by accident; the legacy tree stored limits as bare decimals and a USD limit was once compared
/// against a BDT balance. As a null it means "no limit has been granted", which is <b>not</b> unlimited:
/// <see cref="CreditLimitRule"/> refuses a credit sale when no limit exists instead of treating the absence
/// of a decision as a permissive one. A zero limit is a different and legitimate state — cash before
/// dispatch — and is stored as zero, not as null.
/// </remarks>
public sealed record CustomerProfile
{
    public CustomerProfile(LedgerAccountReference receivableAccount, Money? creditLimit = null, PaymentTerm? defaultPaymentTerm = null)
    {
        ArgumentNullException.ThrowIfNull(receivableAccount);

        if (receivableAccount.SubType != Ostagar.Ledger.AccountSubType.Receivable)
        {
            throw new ArgumentException(
                $"A customer's account must be a receivable, not '{receivableAccount.SubType}'.",
                nameof(receivableAccount));
        }

        if (creditLimit is { } limit && limit.IsNegative)
        {
            throw new ArgumentException(
                $"A credit limit of {limit} is not a limit. Use zero for cash-before-dispatch.",
                nameof(creditLimit));
        }

        ReceivableAccount = receivableAccount;
        CreditLimit = creditLimit;
        DefaultPaymentTerm = defaultPaymentTerm;
    }

    /// <summary>Where this partner's outstanding invoices sit. Debit-normal, because it is an asset.</summary>
    public LedgerAccountReference ReceivableAccount { get; }

    /// <summary>The most this partner may owe at one time. Null means no limit granted, which refuses credit rather than allowing unlimited credit.</summary>
    public Money? CreditLimit { get; }

    /// <summary>
    /// The term a sales document inherits when it states none. Null is not "no terms": a document with no
    /// term and no default must refuse to post rather than invent one.
    /// </summary>
    public PaymentTerm? DefaultPaymentTerm { get; }
}
