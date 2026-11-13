# 14 — Known gaps and review findings

Honest status from a full code review (2026-11-13). Items marked **bug** are defects
found in the code; **gap** is missing-but-planned; **decision** is deliberate.

## Bugs found in review

1. **`Currency.MinorDigitsFor` maps `PKR → 0` minor digits** (`src/Core/Currency.cs`).
   PKR has 2 decimal places (paisa) in practice — the table groups it with
   JPY/KRW/VND. Any PKR-denominated amount would be truncated to whole rupees.
   Likely copy-paste from the zero-decimal list. **Bug.**
2. **Journal numbering is per-journal, not per (journal, period)** as MASTER_PLAN
   I4 states. `Journal.NextNumber` is a single counter; `UQ_gl_entries_number`
   includes `period_id` but the sequence never resets per period — numbers are
   `PREFIX-NNNNNN` continuous across periods. Arguably *stronger* than the plan
   (fewer sequences to audit), but it's a deviation worth recording. **Decision/deviation.**
3. **`RlsSafetyProbe.ModelOnlyContext` hardcodes a placeholder connection string**
   (`Host=localhost;…;Username=garments;Password=garments`) — harmless (model is
   built without connecting) but reads like a credential; also shows the `garments_*`
   naming survived the rename. Cosmetic.

## Legacy naming that survived the Ostagar rename

- DB roles **`garments_owner` / `garments_app`** in RLS migrations +
  `assert-row-level-security.sql`.
- Config section **`GarmentsDatabase`** (`OstagarDatabaseOptions.SectionName`).
- Extension methods **`AddGarmentsData/Cors/Health/Observability`**,
  **`MapGarmentsHealth`**.
- `OstagarDbContextFactory` env var comment, `garments-verify-pg` container name in
  verify-all.sh.

All functional; cosmetic debt. A rename touches migrations (role names are baked
into policies) — needs a deliberate migration, not a find/replace.

## Gaps (planned, not yet built)

- **No CSV import** — the P3 gate's "client CSV → balanced GL → tied trial balance"
  is not done; no `csv` references anywhere in `src/`.
- **No bank module surface** — BANK journal exists; no accounts/transfers/
  reconciliation endpoints.
- **Reporting is started, not done** — trial balance exists
  (`GET /api/v1/ledger/trial-balance`, a read projection over posted GL); P&L, BS,
  cash flow, aging and the `Verifier` don't exist yet.
- **Period close is manual locks only** — `POST .../periods/{id}/locks` moves
  soft/hard/tax locks (forward-only, audited); no `IsClosed` flag management and no
  unlock path exist yet.
- **Permission enforcement handler missing** — `(resource,action)` catalogue +
  role grants exist, but endpoints check `Roles` claims, not permission keys.
  `IPermissionRequirement` is the documented missing half.
- **Module persistence** — DocumentWorkflow and Partners are domain-only; no
  DbSets/migrations for their documents yet (P6).
- **Empty modules** — Inventory, Purchasing, Sales, Payroll, Production are csproj
  shells.
- **Frontend** — only login + placeholder dashboard; no screens for journals,
  cash book, partners; no route protection visible on `/admin/*`; ~28 i18n keys.
- **Storno** — deferred deliberately (would weaken I2); recorded in MASTER_PLAN.
- **`AllowsDrafts`/`UsesStorno` on Journal** — carried, unhonored.
- **`AnalysisId` dimension** — column exists; dimensional reporting is P4+.
- **AccountResolver / determination rules** — `RuleVersionId` column exists;
  resolver lands with P6 posting sources.
- **Audit before/after payloads** — trail records actor/action/outcome/detail but
  not row values either side of change (planned once P3+ write paths settle).

## Deliberate decisions often mistaken for gaps

- Manual journals: `SourceId=null` → no idempotency (each POST is a distinct act).
- LedgerJournalsController: domestic-currency lines only via HTTP (foreign-currency
  pricing exists in the poster, not exposed in the DTO yet).
- `TenantDirectoryDbContext` has no interceptors — must work pre-tenancy.
- Identity join tables: RLS+check but **no** query filter (cascade deletes).
- `X-Forwarded-For` ignored for audit `SourceAddress` (spoofable; needs configured
  trusted-proxy count — deployment decision).
- `MapInboundClaims=false` is load-bearing, not cosmetic.
- Bangla CoA names flagged for native review before pilot (L17 true from start,
  but subtle terminology risk acknowledged).

## Open decisions (MASTER_PLAN §8)

Tax regime (blocks P6) · brand aesthetic at P4 gate (DESIGN.md now answers this) ·
Bangla string inventory per screen · two pilot factory identities · v2 billing.
