using Ostagar.Core;

namespace Ostagar.Modules.DocumentWorkflow;

/// <summary>
/// Hands a document its number, and is the only way a module may ask for one.
/// </summary>
/// <remarks>
/// Declared here and implemented beside the database context, exactly as the ledger declares
/// <see cref="Ostagar.Ledger.IPoster"/>: taking a number is a domain decision whose every step is a
/// storage step. The implementation's whole job is to bind
/// <see cref="DocumentNumberAllocator.Allocate"/> to one transaction — select the sequence row for this
/// tenant, document type and period with <c>SELECT … FOR UPDATE</c>, call the allocator, persist the
/// sequence it returns, and write the document with the number it returned. Invariant I4's database-side
/// twin (a unique index on tenant, document type and number) backs this up, so a caller that skips this
/// interface gets an index violation instead of a readable error, and the rule does not depend on anyone
/// remembering to call it.
/// </remarks>
public interface IDocumentNumberAllocator
{
    /// <summary>
    /// Allocates the number for one document, or explains why there is none. Never throws for a rule the
    /// document simply fails; the refusal comes back on the decision.
    /// </summary>
    /// <param name="tenantId">The tenant whose scheme is being read. Declared rather than ambient, so a
    /// background job or a CLI import cannot allocate from whichever tenant the request context happens
    /// to be holding.</param>
    /// <param name="documentType">Which document type needs a number.</param>
    /// <param name="request">The allocation key, the document date and the expected counter.</param>
    /// <param name="cancellationToken">Cancellation of the waiting, not of the decision.</param>
    Task<NumberingDecision> AllocateAsync(
        TenantId tenantId,
        DocumentTypeCode documentType,
        NumberingAllocationRequest request,
        CancellationToken cancellationToken = default);
}
