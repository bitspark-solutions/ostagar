using Ostagar.Core;
using Ostagar.Modules.DocumentWorkflow;

namespace Ostagar.Tests.Modules.DocumentWorkflow;

/// <summary>
/// The approval rules: who may approve, what an approval freezes, what a posted state refuses, and how a
/// decision is corrected without rewriting it.
/// </summary>
public class DocumentWorkflowEngineTests
{
    private static readonly TenantId Tenant = TenantId.From(
        Guid.Parse("99999999-9999-9999-9999-999999999999"));

    private static readonly Guid DocumentId = Guid.Parse("11111111-2222-3333-4444-555555555555");

    /// <summary>The person who raised the document.</summary>
    private static readonly Guid Maker = Guid.Parse("aaaa0000-0000-4000-8000-000000000001");

    /// <summary>Somebody else, who is allowed to approve it.</summary>
    private static readonly Guid SecondPairOfHands = Guid.Parse("aaaa0000-0000-4000-8000-000000000002");

    private static readonly DateTimeOffset Moment = new(2026, 10, 15, 9, 30, 0, TimeSpan.FromHours(6));

    [Fact]
    public void The_creator_cannot_approve_their_own_document()
    {
        var decision = DocumentWorkflowEngine.Evaluate(
            StandardDocumentWorkflows.PurchaseOrder,
            PendingBy(Maker),
            Ask(Maker, DocumentStateCodes.Approved));

        Assert.Equal(WorkflowDecisionStatus.Refused, decision.Status);
        Assert.Contains(WorkflowErrorCode.SelfApprovalRefused, decision.Errors.Select(error => error.Code));
        Assert.Null(decision.EntryToAppend);
    }

    [Fact]
    public void Somebody_else_can_approve_it_and_the_decision_is_written()
    {
        var decision = DocumentWorkflowEngine.Evaluate(
            StandardDocumentWorkflows.PurchaseOrder,
            PendingBy(Maker),
            Ask(SecondPairOfHands, DocumentStateCodes.Approved));

        Assert.True(decision.Accepted);
        Assert.Equal(DocumentStateCodes.Approved, decision.NewState!.Code);

        var entry = decision.EntryToAppend!;
        Assert.Equal(DocumentStateCodes.PendingApproval, entry.FromState);
        Assert.Equal(DocumentStateCodes.Approved, entry.ToState);
        Assert.Equal(SecondPairOfHands, entry.ActorUserId);
        Assert.Equal(Moment, entry.OccurredAt);
        Assert.Equal(2, entry.EntryNumber);

        // The engine does not mint identifiers: the id belongs to the row the store writes.
        Assert.Equal(Guid.Empty, entry.Id);
    }

    [Fact]
    public void An_approved_document_is_not_editable_but_can_be_sent_back()
    {
        Assert.False(DocumentEditPolicy.AllowsEdit(StandardDocumentStates.Approved.Stage));
        Assert.NotNull(DocumentEditPolicy.RefusalFor(StandardDocumentStates.Approved.Stage));

        // A posted document is frozen by the same policy and a stronger reason, so both refuse the edit form.
        Assert.False(DocumentEditPolicy.AllowsEdit(StandardDocumentStates.Posted.Stage));

        Assert.True(DocumentEditPolicy.AllowsEdit(StandardDocumentStates.Returned.Stage));
        Assert.Null(DocumentEditPolicy.RefusalFor(StandardDocumentStates.Draft.Stage));

        var sendBack = DocumentWorkflowEngine.Evaluate(
            StandardDocumentWorkflows.PurchaseOrder,
            Subject(DocumentStateCodes.Approved, ApprovalTrail()),
            Ask(Maker, DocumentStateCodes.Returned, "Quantities were wrong; the vendor re-quoted."));

        Assert.True(sendBack.Accepted);
        Assert.Equal(DocumentStateCodes.Returned, sendBack.NewState!.Code);
    }

    [Fact]
    public void A_posted_document_refuses_every_transition_it_is_offered()
    {
        var history = PostedTrail();

        foreach (var target in StandardDocumentWorkflows.PostingStyleStates)
        {
            var decision = DocumentWorkflowEngine.Evaluate(
                StandardDocumentWorkflows.SalesInvoice,
                Subject(DocumentStateCodes.Posted, history, DocumentTypeCode.SalesInvoice),
                Ask(SecondPairOfHands, target.Code, "Whatever the reason, none of these are allowed."));

            Assert.Equal(WorkflowDecisionStatus.Refused, decision.Status);
            Assert.Contains(WorkflowErrorCode.TerminalStateRefused, decision.Errors.Select(error => error.Code));
        }
    }

    [Fact]
    public void The_refusal_from_a_posted_state_names_the_document_type_that_carries_the_correction()
    {
        var decision = DocumentWorkflowEngine.Evaluate(
            StandardDocumentWorkflows.SalesInvoice,
            Subject(DocumentStateCodes.Posted, PostedTrail(), DocumentTypeCode.SalesInvoice),
            Ask(SecondPairOfHands, DocumentStateCodes.Draft, "The invoice amount was wrong."));

        // "Terminal" with no stated way out is a dead end rather than a rule, so the refusal says what the
        // accountant must do next: raise a credit note, exactly as the ledger demands a reversal entry.
        Assert.Contains("CREDIT_NOTE", decision.Errors[0].Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_decision_is_corrected_by_appending_a_second_entry_that_links_to_the_first()
    {
        var submitted = Entry(
            1,
            DocumentStateCodes.Draft,
            DocumentStateCodes.PendingApproval,
            Maker,
            DocumentTypeCode.PurchaseOrder);

        var originalApproval = Entry(
            2,
            DocumentStateCodes.PendingApproval,
            DocumentStateCodes.Approved,
            SecondPairOfHands,
            DocumentTypeCode.PurchaseOrder);

        var subject = Subject(DocumentStateCodes.Approved, [submitted, originalApproval]);

        var correction = DocumentWorkflowEngine.Evaluate(
            StandardDocumentWorkflows.PurchaseOrder,
            subject,
            new DocumentWorkflowTransitionRequest
            {
                ActorUserId = SecondPairOfHands,
                ToStateCode = DocumentStateCodes.Returned,
                OccurredAt = Moment.AddMinutes(20),
                Reason = "Approved against the wrong budget line; sent back.",
                CorrectsEntryId = originalApproval.Id,
            });

        Assert.True(correction.Accepted);

        var appended = correction.EntryToAppend!;
        Assert.Equal(originalApproval.Id, appended.CorrectsEntryId);
        Assert.Equal(3, appended.EntryNumber);
        Assert.Equal(DocumentStateCodes.Approved, appended.FromState);
        Assert.Equal(DocumentStateCodes.Returned, appended.ToState);

        // The trail as the store will hold it: the original decision untouched, the correction beside it.
        var trail = new List<DocumentWorkflowHistoryEntry>(subject.History) { appended };

        Assert.Equal(3, trail.Count);
        Assert.Equal(originalApproval, trail[1]);
        Assert.Equal(DocumentStateCodes.Approved, trail[1].ToState);
        Assert.Null(trail[1].CorrectsEntryId);
        Assert.NotSame(originalApproval, appended);
    }

    [Fact]
    public void Correcting_a_decision_that_was_never_recorded_is_refused()
    {
        var decision = DocumentWorkflowEngine.Evaluate(
            StandardDocumentWorkflows.PurchaseOrder,
            Subject(DocumentStateCodes.Approved, ApprovalTrail()),
            new DocumentWorkflowTransitionRequest
            {
                ActorUserId = SecondPairOfHands,
                ToStateCode = DocumentStateCodes.Returned,
                OccurredAt = Moment,
                Reason = "Reversing an approval that does not exist.",
                CorrectsEntryId = Guid.Parse("dddd0000-0000-4000-8000-000000000077"),
            });

        Assert.Equal(WorkflowDecisionStatus.Refused, decision.Status);
        Assert.Contains(WorkflowErrorCode.HistoryEntryNotFound, decision.Errors.Select(error => error.Code));
    }

    [Fact]
    public void A_send_back_without_a_reason_is_refused()
    {
        var decision = DocumentWorkflowEngine.Evaluate(
            StandardDocumentWorkflows.PurchaseOrder,
            Subject(DocumentStateCodes.Approved, ApprovalTrail()),
            Ask(SecondPairOfHands, DocumentStateCodes.Returned, "   "));

        Assert.Equal(WorkflowDecisionStatus.Refused, decision.Status);
        Assert.Contains(WorkflowErrorCode.ReasonRequired, decision.Errors.Select(error => error.Code));
    }

    [Fact]
    public void An_undeclared_path_is_refused_rather_than_invented()
    {
        var decision = DocumentWorkflowEngine.Evaluate(
            StandardDocumentWorkflows.PurchaseOrder,
            Draft(),
            Ask(SecondPairOfHands, DocumentStateCodes.Approved));

        Assert.Equal(WorkflowDecisionStatus.Refused, decision.Status);
        Assert.Contains(WorkflowErrorCode.TransitionNotDeclared, decision.Errors.Select(error => error.Code));
    }

    [Fact]
    public void A_decision_with_no_actor_is_refused()
    {
        var decision = DocumentWorkflowEngine.Evaluate(
            StandardDocumentWorkflows.PurchaseOrder,
            PendingBy(Maker),
            Ask(Guid.Empty, DocumentStateCodes.Approved));

        Assert.Contains(WorkflowErrorCode.MissingActor, decision.Errors.Select(error => error.Code));
    }

    [Fact]
    public void A_state_column_that_disagrees_with_its_own_history_is_refused()
    {
        // Someone edited the status field directly. The trail says the document is still pending, so every
        // rule keyed on "approved" would be answering from a value nobody agreed to.
        var decision = DocumentWorkflowEngine.Evaluate(
            StandardDocumentWorkflows.PurchaseOrder,
            Subject(DocumentStateCodes.Approved, [Entry(1, DocumentStateCodes.Draft, DocumentStateCodes.PendingApproval, Maker, DocumentTypeCode.PurchaseOrder)]),
            Ask(SecondPairOfHands, DocumentStateCodes.Cancelled, "Because the state column says so."));

        Assert.Equal(WorkflowDecisionStatus.Refused, decision.Status);
        Assert.Contains(WorkflowErrorCode.HistoryDoesNotMatchState, decision.Errors.Select(error => error.Code));
    }

    [Fact]
    public void A_new_document_may_only_claim_its_initial_state()
    {
        var decision = DocumentWorkflowEngine.Evaluate(
            StandardDocumentWorkflows.PurchaseOrder,
            Subject(DocumentStateCodes.Approved, []),
            Ask(SecondPairOfHands, DocumentStateCodes.Cancelled, "No trail at all."));

        Assert.Contains(WorkflowErrorCode.HistoryDoesNotMatchState, decision.Errors.Select(error => error.Code));
    }

    [Fact]
    public void A_definition_that_leaves_posted_non_terminal_is_refused_by_the_engine()
    {
        var broken = new DocumentWorkflowDefinition
        {
            DocumentType = DocumentTypeCode.SalesInvoice,
            InitialStateCode = DocumentStateCodes.Draft,
            States =
            [
                StandardDocumentStates.Draft,
                new DocumentStateDefinition(
                    DocumentStateCodes.Posted,
                    DocumentLifecycleStage.Posted,
                    new BilingualLabel("Posted", "খতিরে জমা")),
            ],
            Transitions = [new DocumentTransition(DocumentStateCodes.Draft, DocumentStateCodes.Posted)],
            ReversalDocumentType = DocumentTypeCode.CreditNote,
        };

        var decision = DocumentWorkflowEngine.Evaluate(
            broken,
            Subject(DocumentStateCodes.Draft, [], DocumentTypeCode.SalesInvoice),
            Ask(SecondPairOfHands, DocumentStateCodes.Posted));

        Assert.Equal(WorkflowDecisionStatus.Refused, decision.Status);
        Assert.Contains(
            WorkflowErrorCode.PostedStageMustBeTerminal,
            decision.Errors.Select(error => error.Code));
    }

    private static DocumentWorkflowTransitionRequest Ask(Guid actor, string to, string reason = "") =>
        new()
        {
            ActorUserId = actor,
            ToStateCode = to,
            OccurredAt = Moment,
            Reason = reason,
        };

    private static IReadOnlyList<DocumentWorkflowHistoryEntry> ApprovalTrail() =>
        [
            Entry(1, DocumentStateCodes.Draft, DocumentStateCodes.PendingApproval, Maker, DocumentTypeCode.PurchaseOrder),
            Entry(2, DocumentStateCodes.PendingApproval, DocumentStateCodes.Approved, SecondPairOfHands, DocumentTypeCode.PurchaseOrder),
        ];

    private static IReadOnlyList<DocumentWorkflowHistoryEntry> PostedTrail() =>
        [
            Entry(1, DocumentStateCodes.Draft, DocumentStateCodes.PendingApproval, Maker, DocumentTypeCode.SalesInvoice),
            Entry(2, DocumentStateCodes.PendingApproval, DocumentStateCodes.Approved, SecondPairOfHands, DocumentTypeCode.SalesInvoice),
            Entry(3, DocumentStateCodes.Approved, DocumentStateCodes.Posted, SecondPairOfHands, DocumentTypeCode.SalesInvoice),
        ];

    /// <summary>
    /// A persisted decision, with an id because only the store assigns one and this is a row that has
    /// already been written.
    /// </summary>
    private static DocumentWorkflowHistoryEntry Entry(
        int number,
        string from,
        string to,
        Guid actor,
        DocumentTypeCode documentType,
        Guid? correctsEntryId = null) =>
        new()
        {
            Id = Guid.Parse(string.Create(System.Globalization.CultureInfo.InvariantCulture, $"00000000-0000-4000-8000-{number:D12}")),
            TenantId = Tenant,
            DocumentType = documentType,
            DocumentId = DocumentId,
            EntryNumber = number,
            FromState = from,
            ToState = to,
            ActorUserId = actor,
            OccurredAt = Moment.AddMinutes(number),
            CorrectsEntryId = correctsEntryId,
        };

    private static DocumentWorkflowSubject Draft() =>
        Subject(DocumentStateCodes.Draft, [], DocumentTypeCode.PurchaseOrder);

    private static DocumentWorkflowSubject PendingBy(Guid creator) =>
        Subject(
            DocumentStateCodes.PendingApproval,
            [Entry(1, DocumentStateCodes.Draft, DocumentStateCodes.PendingApproval, creator, DocumentTypeCode.PurchaseOrder)],
            DocumentTypeCode.PurchaseOrder);

    private static DocumentWorkflowSubject Subject(
        string stateCode,
        IReadOnlyList<DocumentWorkflowHistoryEntry> history,
        DocumentTypeCode? documentType = null) =>
        new()
        {
            TenantId = Tenant,
            DocumentType = documentType ?? DocumentTypeCode.PurchaseOrder,
            DocumentId = DocumentId,
            CurrentStateCode = stateCode,
            CreatorActorId = Maker,
            History = history,
        };
}
