# 08 — Document workflow module (`src/Modules/DocumentWorkflow`)

Pure domain library (46 files, ~2,750 LOC) for **business document lifecycles** —
purchase orders, invoices, bills, credit notes, payroll runs. Nothing here touches a
database; the integrator owns persistence and transactions.

## Two separable concerns

### 1. Workflow engine — "may this document move?"

`DocumentWorkflowEngine.Evaluate(definition, subject, request)` → `DocumentWorkflowDecision`.
The **single place** any module asks whether a transition is allowed — same answer on
screen, in batch jobs, and in tests.

Core types:

- `DocumentWorkflowDefinition` — declared lifecycle: `DocumentType`, states
  (`DocumentStateDefinition`: code, terminal flag, labels), transitions
  (`DocumentTransition`: from→to, `Actor` requirement, `RequiresReason`), optional
  `ReversalDocumentType` (the correcting doc type for terminal states).
- `DocumentWorkflowSubject` — the document's current position: type, id, current
  state code, creator id, tenant, recorded `History`.
- `DocumentWorkflowTransitionRequest` — actor, target state, reason, timestamp,
  optional `CorrectsEntryId`.
- `DocumentWorkflowHistoryEntry` — append-only decision record: entry number, from→to,
  actor, time, reason.
- `StandardDocumentStates` / `StandardDocumentWorkflows` — the standard lifecycle
  catalog (draft → submitted → approved → posted → closed/cancelled shapes, with
  Bangla `BilingualLabel`s).
- `DocumentTypeCode`, `DocumentStateCodes`, `DocumentLifecycleStage`,
  `DocumentEditPolicy`, `DocumentLanguage`, `DocumentPrintReadiness`,
  `PrintableDocument`, `PrintDocumentLine`, `WorkflowActorRequirement`
  (`AnyActor`/`NotDocumentCreator` — segregation of duties).

Rules enforced (each maps to a real failure):
- Definition must validate; document type must match definition.
- Current state must be a declared state; target must be declared.
- **Terminal states refuse** — correction is a new reversal-type document (same rule
  as ledger I3: "a path nobody wrote down is not a path").
- Transition must be **declared** — no undeclared moves.
- **Self-approval refused** when transition requires `NotDocumentCreator` — "an
  approval from the person who made the commitment is the same signature twice."
- **Reason required** where declared — moves a later reader can't judge.
- `CorrectsEntryId` must reference a real history entry.
- Missing actor refused — "a decision with no actor is a blank line on the paper."

On accept, `Decision.EntryToAppend` carries the history entry — state change and
history **must commit together** (an approval without its record is unprovable).

### 2. Document numbering — gapless business numbers

`DocumentNumberAllocator` — pure function, no SQL:

- `OpenSequence(scheme, period, tenantId)` — opens a period counter at the scheme's
  `StartingValue`.
- `Allocate(scheme, sequence, request)` → `NumberingDecision` (number + counter to
  save) or numbered errors.

Distinct from **journal numbers**: journal numbers mint at post time and prove money
moved; document numbers mint at paper creation and prove the paper was registered.
An unapproved PO must not consume a voucher number — the two auditor questions
("which vouchers are missing" / "which orders were never registered") stay separate.

Key types: `DocumentNumberingScheme` (format, period component, fiscal start month,
start value, active flag), `NumberingFormat`, `NumberingPeriodKey`/`NumberingPeriodComponent`
(none/month/quarter/year/fiscal variants), `NumberingSequenceState` (counter row:
`NextValue`, `IssuedCount`), `NumberingAllocationRequest` (`AllocationKey` — makes a
retry distinguishable from a second document), `NumberingSequenceInspector` +
`NumberingGapReport` (audit: find missing numbers), `NumberingSchemeEditValidator` +
`NumberingSchemeRules` (scheme changes can't invalidate issued numbers),
`NumberingValueRange`, `NumberingPeriodComponentLabels` (bilingual period labels).

**No gaps by construction**: counter advances exactly one per allocation; there is no
`Release`/`Free`/`Recycle`/`Renumber` — a cancelled document keeps its number, because
a gap from deletion is indistinguishable from a document never recorded.

The integrator's job is exactly one transaction: lock the counter row → `Allocate` →
write document + save counter. `sequence.Id` stays empty in the domain result — ids
belong to the store, not the function.

## Print readiness

`DocumentPrintReadiness` + `PrintableDocument`/`PrintDocumentLine` — decides whether
a document may be printed (all required fields present, state allows printing) and
what the printable payload is. `BilingualLabel` carries en/bn label pairs so printed
documents match the reader's language.

## Status

Domain-complete and unit-tested (`Ostagar.Tests.Modules.DocumentWorkflow`: engine,
allocator, sequence inspector, scheme edit validator, print readiness, standard
workflows). **Not yet wired** to any HTTP endpoint or persistence — it's the library
the P6 order-to-cash/procure-to-pay cycles will build on.
