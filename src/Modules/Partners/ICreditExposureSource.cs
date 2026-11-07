using Ostagar.Core;

namespace Ostagar.Modules.Partners;

/// <summary>
/// Reads what a customer already owes, so the credit check compares against the books rather than against a
/// balance this module keeps for itself.
/// </summary>
/// <remarks>
/// <para>
/// This is the plan's central rule applied to credit: a module never computes a financial figure from its own
/// tables. Partners hold no running balance, because a second balance is a second answer, and the one that is
/// wrong is discovered by the owner when the ageing report and the order screen disagree — at which point they
/// stop trusting both.
/// </para>
///
/// <para>
/// The implementation is the P4 aged-receivables projection over the ledger, which is also what makes the
/// number auditable: whatever <see cref="CurrentExposureAsync"/> returns must be reproducible from the posted
/// entries at the same as-of date, and the verifier's control totals are what prove it.
/// </para>
///
/// <para>
/// The currency is a parameter and the returned <see cref="Money"/> must be in it. A taka exposure converted
/// into a dollar limit is the exact class of silent currency mixing this product's money type exists to make
/// unrepresentable, so conversion — with its rate and its effective date — is the projection's decision and is
/// stated there, not inferred here.
/// </para>
/// </remarks>
public interface ICreditExposureSource
{
    /// <summary>
    /// The customer's outstanding receivable as at <paramref name="asOf"/>, expressed in
    /// <paramref name="currency"/>. Zero when nothing is outstanding, which is a real answer rather than an
    /// absent one.
    /// </summary>
    Task<Money> CurrentExposureAsync(
        PartnerId customer,
        Currency currency,
        DateOnly asOf,
        CancellationToken cancellationToken = default);
}
