namespace Ostagar.Modules.DocumentWorkflow;

/// <summary>
/// The shared state definitions, each with the English and Bangla name a printed document shows.
/// </summary>
/// <remarks>
/// One list, used by every standard workflow, so that "approved" looks the same on a purchase order and a
/// payroll run and means the same to <see cref="DocumentEditPolicy"/>. Keeping the labels here rather than
/// in the web message bundles is deliberate: a state name is printed on paper that outlives wording
/// changes, and a translation table is the wrong place for a fact about a document.
///
/// The Bangla forms are the working wording for the pilot factories. The plan already books a native
/// review of accounting terms for the P9 Bangla assets pass, because a subtly wrong bookkeeping word on an
/// owner's screen is worse than an English one they can read; what matters here is that no state can exist
/// without a Bangla form at all, which <see cref="DocumentWorkflowDefinition.Validate"/> and
/// <see cref="DocumentPrintReadiness"/> both enforce.
/// </remarks>
public static class StandardDocumentStates
{
    /// <summary>Free to edit; nothing promised.</summary>
    public static DocumentStateDefinition Draft { get; } = new(
        DocumentStateCodes.Draft,
        DocumentLifecycleStage.Draft,
        new BilingualLabel("Draft", "খসড়া"));

    /// <summary>Submitted; waiting on somebody else.</summary>
    public static DocumentStateDefinition PendingApproval { get; } = new(
        DocumentStateCodes.PendingApproval,
        DocumentLifecycleStage.PendingApproval,
        new BilingualLabel("Pending approval", "অনুমোদনের অপেক্ষায়"));

    /// <summary>Agreed by a second pair of hands; content frozen.</summary>
    public static DocumentStateDefinition Approved { get; } = new(
        DocumentStateCodes.Approved,
        DocumentLifecycleStage.Approved,
        new BilingualLabel("Approved", "অনুমোদিত"));

    /// <summary>In the ledger, and nothing can move it again.</summary>
    public static DocumentStateDefinition Posted { get; } = new(
        DocumentStateCodes.Posted,
        DocumentLifecycleStage.Posted,
        new BilingualLabel("Posted", "খতিরে জমা"),
        IsTerminal: true);

    /// <summary>Back with the maker, with a reason on record.</summary>
    public static DocumentStateDefinition Returned { get; } = new(
        DocumentStateCodes.Returned,
        DocumentLifecycleStage.Returned,
        new BilingualLabel("Returned for amendment", "সংশোধনের জন্য ফেরত"));

    /// <summary>Refused. Revivable only where the definition declares the path.</summary>
    public static DocumentStateDefinition Rejected { get; } = new(
        DocumentStateCodes.Rejected,
        DocumentLifecycleStage.Rejected,
        new BilingualLabel("Rejected", "প্রত্যাখ্যাত"));

    /// <summary>Withdrawn. Keeps the number it consumed.</summary>
    public static DocumentStateDefinition Cancelled { get; } = new(
        DocumentStateCodes.Cancelled,
        DocumentLifecycleStage.Cancelled,
        new BilingualLabel("Cancelled", "বাতিল"),
        IsTerminal: true);

    /// <summary>Every shared state, in the order a settings screen lists them.</summary>
    public static IReadOnlyList<DocumentStateDefinition> All { get; } =
    [
        Draft,
        PendingApproval,
        Approved,
        Posted,
        Returned,
        Rejected,
        Cancelled,
    ];
}
