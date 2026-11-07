namespace Ostagar.Modules.DocumentWorkflow;

/// <summary>
/// Who is allowed to perform a transition, declared on the transition rather than written where it is
/// used.
/// </summary>
/// <remarks>
/// Segregation of duties in a garment factory is a naming problem, not a permission problem: the owner
/// approves their own purchase order because the owner created it, and the paper says "approved" as
/// truthfully as any other. The rule that stops that has to live on the transition so every document type
/// inherits it, and it has to be one of a small declared set so a reviewer can see it without reading a
/// service.
/// </remarks>
public enum WorkflowActorRequirement
{
    /// <summary>Any authenticated actor of the tenant. Used for moves like cancel-by-creator.</summary>
    None,

    /// <summary>
    /// The actor must not be the one who created the document. This is the approval rule: an approval from
    /// the person who made the commitment is not an approval, it is the same signature twice.
    /// </summary>
    NotDocumentCreator,
}
