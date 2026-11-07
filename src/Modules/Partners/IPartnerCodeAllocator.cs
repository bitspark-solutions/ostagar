using Ostagar.Core;

namespace Ostagar.Modules.Partners;

/// <summary>
/// Issues partner codes. The only way a code enters this system.
/// </summary>
/// <remarks>
/// <para>
/// The rule this exists to enforce is that a code is a sequence value, not a fact about a name. Nothing in
/// this module can produce a <see cref="PartnerCode"/> from text: there is no allocation from a name, no
/// random component, and no "leave it blank and we'll fill it in" path — the legacy tree's
/// <c>new Random().Next(100, 999)</c> codes and its <c>GetOrCreateCategoryAsync</c> from free text are both
/// shapes this interface refuses to be.
/// </para>
///
/// <para>
/// <b>Implementation belongs to the document-numbering agent</b> (see "needs integration"), whose sequence
/// service already mints gapless per-tenant numbers under a lock for journals. Two properties of that design
/// are required here rather than assumed: allocation is unique per tenant, and it is safe under two clerks
/// saving at the same moment. A partner's code may be re-issued after a void only if the numbering service
/// allows it — journal numbers may not be reused (invariant I4) but a partner code that named nothing is not a
/// legal sequence, and this module takes whichever decision the numbering service makes.
/// </para>
///
/// <para>
/// <b>One code per partner, never one per role.</b> A per-role sequence would let the same trading house be
/// allocated <c>CUS-0142</c> and <c>VEN-0088</c>, and the two records would then be the duplicate the check
/// exists to catch. The role is granted after allocation, on the same identity.
/// </para>
/// </remarks>
public interface IPartnerCodeAllocator
{
    /// <summary>
    /// Allocates the next code for this tenant. The call has a side effect (the sequence advances) and is not
    /// idempotent, so callers must allocate once per partner they are about to persist.
    /// </summary>
    Task<PartnerCode> NextAsync(
        TenantId tenant,
        CancellationToken cancellationToken = default);
}
