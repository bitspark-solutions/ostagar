using Ostagar.Core;
using Ostagar.Modules.DocumentWorkflow;

namespace Ostagar.Tests.Modules.DocumentWorkflow;

/// <summary>
/// The standard definitions checked the way the plan needs them to be: valid, bilingual, and carrying the
/// approval rule everywhere rather than in the one module someone remembered.
/// </summary>
public class StandardDocumentWorkflowsTests
{
    private static readonly TenantId Tenant = TenantId.From(
        Guid.Parse("15151515-0000-4000-8000-000000000001"));
    [Fact]
    public void Every_standard_definition_is_valid()
    {
        foreach (var definition in StandardDocumentWorkflows.Definitions)
        {
            var errors = definition.Validate();

            Assert.Empty(errors);
        }
    }

    [Fact]
    public void Every_state_a_standard_document_can_print_has_a_Bangla_name()
    {
        // Law L17 on a printed document: the state name the counterparty reads must exist in the language
        // the factory runs in. A missing Bangla form is caught here before any screen can offer it.
        var missing = new List<string>();

        foreach (var definition in StandardDocumentWorkflows.Definitions)
        {
            foreach (var state in definition.States)
            {
                if (string.IsNullOrWhiteSpace(state.Label.Bangla)
                    || string.IsNullOrWhiteSpace(state.Label.English))
                {
                    missing.Add($"{definition.DocumentType}:{state.Code}");
                }
            }
        }

        Assert.Empty(missing);
    }

    [Fact]
    public void Every_document_type_that_can_be_posted_declares_how_it_is_reversed()
    {
        foreach (var definition in StandardDocumentWorkflows.Definitions)
        {
            if (definition.States.Any(state => state.Stage == DocumentLifecycleStage.Posted))
            {
                Assert.NotNull(definition.ReversalDocumentType);
            }
        }
    }

    [Fact]
    public void Approval_is_never_open_to_the_creator_in_any_standard_definition()
    {
        // The rule this component exists to write once. If a future definition approves without the
        // requirement, this test is the thing that notices.
        foreach (var definition in StandardDocumentWorkflows.Definitions)
        {
            var approve = definition.FindTransition(
                DocumentStateCodes.PendingApproval,
                DocumentStateCodes.Approved);

            Assert.NotNull(approve);
            Assert.Equal(WorkflowActorRequirement.NotDocumentCreator, approve!.Actor);
        }
    }

    [Fact]
    public void Nothing_leaves_a_terminal_state_in_any_standard_definition()
    {
        foreach (var definition in StandardDocumentWorkflows.Definitions)
        {
            foreach (var state in definition.States.Where(state => state.IsTerminal))
            {
                Assert.Empty(definition.TransitionsFrom(state.Code));
            }
        }
    }

    [Fact]
    public void A_posted_state_is_terminal_and_cancelled_is_too()
    {
        Assert.True(StandardDocumentStates.Posted.IsTerminal);
        Assert.True(StandardDocumentStates.Cancelled.IsTerminal);
        Assert.False(StandardDocumentStates.Approved.IsTerminal);
    }

    [Fact]
    public void A_new_document_type_needs_a_definition_and_not_a_new_engine()
    {
        // Proof that the machine is declarative: a tenant-specific type with its own states and paths is
        // evaluated by the same engine, with the same creator rule, and no code change.
        var definition = new DocumentWorkflowDefinition
        {
            DocumentType = DocumentTypeCode.From("FABRIC_ISSUE"),
            InitialStateCode = "ISSUED",
            States =
            [
                new DocumentStateDefinition("ISSUED", DocumentLifecycleStage.Draft, new BilingualLabel("Issued", "ইস্যু")),
                new DocumentStateDefinition("ACCEPTED", DocumentLifecycleStage.Approved, new BilingualLabel("Accepted", "গ্রহণ")),
            ],
            Transitions =
            [
                new DocumentTransition(
                    "ISSUED",
                    "ACCEPTED",
                    WorkflowActorRequirement.NotDocumentCreator),
            ],
        };

        Assert.Empty(definition.Validate());

        var creator = Guid.Parse("12121212-0000-4000-8000-000000000001");
        var approver = Guid.Parse("13131313-0000-4000-8000-000000000001");

        var subject = new DocumentWorkflowSubject
        {
            TenantId = Tenant,
            DocumentType = definition.DocumentType,
            DocumentId = Guid.Parse("14141414-0000-4000-8000-000000000001"),
            CurrentStateCode = "ISSUED",
            CreatorActorId = creator,
        };

        var refused = DocumentWorkflowEngine.Evaluate(
            definition,
            subject,
            new DocumentWorkflowTransitionRequest
            {
                ActorUserId = creator,
                ToStateCode = "ACCEPTED",
                OccurredAt = DateTimeOffset.UnixEpoch,
            });

        var accepted = DocumentWorkflowEngine.Evaluate(
            definition,
            subject,
            new DocumentWorkflowTransitionRequest
            {
                ActorUserId = approver,
                ToStateCode = "ACCEPTED",
                OccurredAt = DateTimeOffset.UnixEpoch,
            });

        Assert.Contains(WorkflowErrorCode.SelfApprovalRefused, refused.Errors.Select(error => error.Code));
        Assert.True(accepted.Accepted);
    }

    [Fact]
    public void A_definition_that_declares_a_way_out_of_posted_is_itself_refused()
    {
        var broken = new DocumentWorkflowDefinition
        {
            DocumentType = DocumentTypeCode.SalesInvoice,
            InitialStateCode = DocumentStateCodes.Draft,
            States = StandardDocumentWorkflows.PostingStyleStates,
            Transitions =
            [
                .. StandardDocumentWorkflows.PostingStyleTransitions,
                new DocumentTransition(DocumentStateCodes.Posted, DocumentStateCodes.Draft),
            ],
            ReversalDocumentType = DocumentTypeCode.CreditNote,
        };

        Assert.Contains(
            WorkflowErrorCode.TransitionFromTerminalState,
            broken.Validate().Select(error => error.Code));
    }

}
