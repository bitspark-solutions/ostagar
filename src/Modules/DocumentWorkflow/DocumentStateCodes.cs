namespace Ostagar.Modules.DocumentWorkflow;

/// <summary>
/// The state codes the standard workflow definitions share, so that one word means one stage in every
/// module.
/// </summary>
/// <remarks>
/// Constants instead of an enum because a tenant may add states of their own and the vocabulary has to
/// stay open, but the shared ones must not be retyped per module — that retyping is how the legacy build
/// ended up with <c>"Approved"</c>, <c>"APPROVED"</c> and <c>"FINALISED"</c> three rules for one fact. A
/// module that writes <see cref="Approved"/> and a module that writes <see cref="PendingApproval"/> are
/// then guaranteed to mean the same stage to
/// <see cref="DocumentWorkflowEngine"/>, <see cref="DocumentEditPolicy"/> and the printer.
/// </remarks>
public static class DocumentStateCodes
{
    public const string Draft = "DRAFT";

    public const string PendingApproval = "PENDING_APPROVAL";

    public const string Approved = "APPROVED";

    /// <summary>Recorded in the ledger. Terminal: only a reversal document follows.</summary>
    public const string Posted = "POSTED";

    /// <summary>Sent back for amendment, with a reason on record.</summary>
    public const string Returned = "RETURNED";

    public const string Rejected = "REJECTED";

    public const string Cancelled = "CANCELLED";
}
