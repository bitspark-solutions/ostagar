using Ostagar.Core;

namespace Ostagar.Tests.Integration;

/// <summary>
/// The ids one provisioned company's ledger needs, so each invariant test writes a posting without
/// re-deriving them.
/// </summary>
/// <param name="TenantId">The company this world belongs to.</param>
/// <param name="Cash">A postable asset account, used for the debit side.</param>
/// <param name="Income">A postable income account, used for the credit side.</param>
/// <param name="PostingDate">A date inside the one open period created with this world.</param>
internal sealed record LedgerWorld(TenantId TenantId, Guid Cash, Guid Income, DateOnly PostingDate);
