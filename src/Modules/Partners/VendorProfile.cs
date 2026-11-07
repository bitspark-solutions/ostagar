namespace Ostagar.Modules.Partners;

/// <summary>
/// The vendor capability on a partner: the payable account and the default payment term for what we owe.
/// </summary>
/// <remarks>
/// Fabric, trim, laundry, embroidery and jobwork suppliers are the parties a garment factory's cash cycle
/// actually turns on, and a supplier's terms are the reason a purchase order can be raised at all — a
/// supplier on full advance is not usable for a purchase order unless the factory has the deposit. Holding
/// this as a capability beside <see cref="CustomerProfile"/> on the same partner is what lets a trading house
/// that buys our output and supplies our fabric be one record with two balances, each on its own side of the
/// ledger.
///
/// There is no credit limit here on purpose: what we owe a supplier is not a risk we cap, and an "advance
/// ceiling" would be a different rule that no one has asked for yet.
/// </remarks>
public sealed record VendorProfile
{
    public VendorProfile(LedgerAccountReference payableAccount, PaymentTerm? defaultPaymentTerm = null)
    {
        ArgumentNullException.ThrowIfNull(payableAccount);

        if (payableAccount.SubType != Ostagar.Ledger.AccountSubType.Payable)
        {
            throw new ArgumentException(
                $"A vendor's account must be a payable, not '{payableAccount.SubType}'.",
                nameof(payableAccount));
        }

        PayableAccount = payableAccount;
        DefaultPaymentTerm = defaultPaymentTerm;
    }

    /// <summary>Where this partner's unpaid bills sit. Credit-normal, because it is a liability.</summary>
    public LedgerAccountReference PayableAccount { get; }

    /// <summary>The term a purchase document inherits when it states none.</summary>
    public PaymentTerm? DefaultPaymentTerm { get; }
}
