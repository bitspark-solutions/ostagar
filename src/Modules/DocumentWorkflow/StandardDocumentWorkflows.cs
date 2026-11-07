namespace Ostagar.Modules.DocumentWorkflow;

/// <summary>
/// The workflow every document type starts from, so a module adopts a definition instead of inventing
/// rules.
/// </summary>
/// <remarks>
/// Two shapes, because that is what the domain actually has. An order shape commits to something and stops
/// before the ledger: sales and purchase orders, delivery notes. A posting shape continues into the ledger
/// and therefore ends at a terminal posted state whose only successor is a reversal document: invoices,
/// bills, notes, work orders, payroll runs.
///
/// Both shapes carry the same three rules, written once here as data rather than four times as
/// <c>if</c> statements: approval belongs to someone other than the creator, an approved document is
/// edited only by sending it back with a reason, and nothing leaves a posted state. A tenant who needs a
/// different path adds a <see cref="DocumentWorkflowDefinition"/> with its own transition list — the
/// engine does not change, so the rules cannot silently differ between modules, which is the failure this
/// whole component exists to prevent.
///
/// Declaration order below is load-bearing: static initialisers run in textual order, so the shared states
/// and transitions are declared before the definitions that use them and the
/// <see cref="Definitions"/> list last of all.
/// </remarks>
public static class StandardDocumentWorkflows
{
    /// <summary>Every state a document that will never reach the ledger can be in.</summary>
    public static IReadOnlyList<DocumentStateDefinition> OrderStyleStates { get; } =
    [
        StandardDocumentStates.Draft,
        StandardDocumentStates.PendingApproval,
        StandardDocumentStates.Approved,
        StandardDocumentStates.Returned,
        StandardDocumentStates.Rejected,
        StandardDocumentStates.Cancelled,
    ];

    /// <summary>Every state a document that posts can be in, including the terminal posted one.</summary>
    public static IReadOnlyList<DocumentStateDefinition> PostingStyleStates { get; } =
    [
        StandardDocumentStates.Draft,
        StandardDocumentStates.PendingApproval,
        StandardDocumentStates.Approved,
        StandardDocumentStates.Posted,
        StandardDocumentStates.Returned,
        StandardDocumentStates.Rejected,
        StandardDocumentStates.Cancelled,
    ];

    private static DocumentTransition Submit { get; } = new(
        DocumentStateCodes.Draft, DocumentStateCodes.PendingApproval);

    private static DocumentTransition Approve { get; } = new(
        DocumentStateCodes.PendingApproval,
        DocumentStateCodes.Approved,
        WorkflowActorRequirement.NotDocumentCreator);

    private static DocumentTransition Post { get; } = new(
        DocumentStateCodes.Approved, DocumentStateCodes.Posted);

    private static DocumentTransition Reject { get; } = new(
        DocumentStateCodes.PendingApproval,
        DocumentStateCodes.Rejected,
        WorkflowActorRequirement.NotDocumentCreator,
        RequiresReason: true);

    private static DocumentTransition ReturnFromPending { get; } = new(
        DocumentStateCodes.PendingApproval,
        DocumentStateCodes.Returned,
        RequiresReason: true);

    private static DocumentTransition ReturnFromApproved { get; } = new(
        DocumentStateCodes.Approved,
        DocumentStateCodes.Returned,
        RequiresReason: true);

    private static DocumentTransition CancelFromDraft { get; } = new(
        DocumentStateCodes.Draft,
        DocumentStateCodes.Cancelled,
        RequiresReason: true);

    private static DocumentTransition CancelFromApproved { get; } = new(
        DocumentStateCodes.Approved,
        DocumentStateCodes.Cancelled,
        RequiresReason: true);

    private static DocumentTransition ReviveAfterRejection { get; } = new(
        DocumentStateCodes.Rejected,
        DocumentStateCodes.Draft,
        RequiresReason: true);

    /// <summary>The moves of the order shape.</summary>
    public static IReadOnlyList<DocumentTransition> OrderStyleTransitions { get; } =
    [
        Submit,
        CancelFromDraft,
        Approve,
        Reject,
        ReturnFromPending,
        ReturnFromApproved,
        CancelFromApproved,
        ReviveAfterRejection,
    ];

    /// <summary>The moves of the posting shape: the order shape plus the one into the ledger.</summary>
    public static IReadOnlyList<DocumentTransition> PostingStyleTransitions { get; } =
    [
        Submit,
        CancelFromDraft,
        Approve,
        Reject,
        ReturnFromPending,
        ReturnFromApproved,
        CancelFromApproved,
        ReviveAfterRejection,
        Post,
    ];

    public static DocumentWorkflowDefinition SalesOrder { get; } = new()
    {
        DocumentType = DocumentTypeCode.SalesOrder,
        InitialStateCode = DocumentStateCodes.Draft,
        States = OrderStyleStates,
        Transitions = OrderStyleTransitions,
    };

    public static DocumentWorkflowDefinition SalesInvoice { get; } = new()
    {
        DocumentType = DocumentTypeCode.SalesInvoice,
        InitialStateCode = DocumentStateCodes.Draft,
        States = PostingStyleStates,
        Transitions = PostingStyleTransitions,
        ReversalDocumentType = DocumentTypeCode.CreditNote,
    };

    public static DocumentWorkflowDefinition CreditNote { get; } = new()
    {
        DocumentType = DocumentTypeCode.CreditNote,
        InitialStateCode = DocumentStateCodes.Draft,
        States = PostingStyleStates,
        Transitions = PostingStyleTransitions,
        ReversalDocumentType = DocumentTypeCode.CreditNote,
    };

    public static DocumentWorkflowDefinition PurchaseOrder { get; } = new()
    {
        DocumentType = DocumentTypeCode.PurchaseOrder,
        InitialStateCode = DocumentStateCodes.Draft,
        States = OrderStyleStates,
        Transitions = OrderStyleTransitions,
    };

    public static DocumentWorkflowDefinition PurchaseBill { get; } = new()
    {
        DocumentType = DocumentTypeCode.PurchaseBill,
        InitialStateCode = DocumentStateCodes.Draft,
        States = PostingStyleStates,
        Transitions = PostingStyleTransitions,
        ReversalDocumentType = DocumentTypeCode.DebitNote,
    };

    public static DocumentWorkflowDefinition DebitNote { get; } = new()
    {
        DocumentType = DocumentTypeCode.DebitNote,
        InitialStateCode = DocumentStateCodes.Draft,
        States = PostingStyleStates,
        Transitions = PostingStyleTransitions,
        ReversalDocumentType = DocumentTypeCode.DebitNote,
    };

    public static DocumentWorkflowDefinition DeliveryNote { get; } = new()
    {
        DocumentType = DocumentTypeCode.DeliveryNote,
        InitialStateCode = DocumentStateCodes.Draft,
        States = OrderStyleStates,
        Transitions = OrderStyleTransitions,
    };

    /// <summary>
    /// A posted work order is corrected by a reversing stock move, because that is the document which
    /// carries the material and the job cost back out of the books.
    /// </summary>
    public static DocumentWorkflowDefinition WorkOrder { get; } = new()
    {
        DocumentType = DocumentTypeCode.WorkOrder,
        InitialStateCode = DocumentStateCodes.Draft,
        States = PostingStyleStates,
        Transitions = PostingStyleTransitions,
        ReversalDocumentType = DocumentTypeCode.StockMove,
    };

    /// <summary>
    /// A posted payroll run is corrected by a further run, which is what a payroll actually is: a period's
    /// figures recomputed and posted again, not last month's row edited.
    /// </summary>
    public static DocumentWorkflowDefinition PayrollRun { get; } = new()
    {
        DocumentType = DocumentTypeCode.PayrollRun,
        InitialStateCode = DocumentStateCodes.Draft,
        States = PostingStyleStates,
        Transitions = PostingStyleTransitions,
        ReversalDocumentType = DocumentTypeCode.PayrollRun,
    };

    /// <summary>Every standard definition, ready to bind to a numbering scheme.</summary>
    public static IReadOnlyList<DocumentWorkflowDefinition> Definitions { get; } =
    [
        SalesOrder,
        SalesInvoice,
        CreditNote,
        PurchaseOrder,
        PurchaseBill,
        DebitNote,
        DeliveryNote,
        WorkOrder,
        PayrollRun,
    ];

    /// <summary>The standard definition for a document type, or null when the tenant declared their own.</summary>
    public static DocumentWorkflowDefinition? For(DocumentTypeCode documentType) =>
        Definitions.FirstOrDefault(definition => definition.DocumentType == documentType);
}
