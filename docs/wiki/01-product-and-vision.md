# 01 — Product and vision

## What Ostagar is

A **multi-tenant, double-entry-first ERP for garment factories** — factories that both
manufacture (cutting → stitching → packing → dispatch) and trade. Functional currency is
BDT, fiscal year is July–June (12 periods), and the operating UI language is Bangla with
English retained for account codes and formal reports.

One-sentence definition (from `docs/MASTER_PLAN.md` §1): *"a multi-tenant,
double-entry-first ERP for garments manufacturing and trading businesses, whose books
are provably correct and whose screen quality reads as a commercial product."*

## Who buys it

The **owner of a garment factory**, small/informal through mid-size. Go-to-market is a
hand-held design-partner pilot, then reference selling — not self-serve SaaS (that's v2).

The five buying reasons, in order: (1) "where is my order?", (2) "what did this style
really cost me", (3) fabric/accessory stock vs committed, (4) daily cash position, (5)
reports an accountant and VAT office accept. The ledger is the *foundation* of 2/4/5 —
the reason customers **stay**, not the reason they buy.

Module quality tiers: **A** (world-class: ledger, cash book, reporting, order progress,
job costing) · **B** (correct SMB: inventory, purchasing, sales, VAT reports) ·
**C** (thin/deferrable: payroll/HR). Explicitly out of v1: APS scheduling, MRP explosion,
QMS, barcode WMS.

## Why a rebuild, not a fix

`legacy/` (frozen at tag `legacy-final`) was audited and found never simultaneously
buildable and runnable:

- 22 of ~35 DI registrations commented out; frontend `npm run build` exited 1.
- Cash book posted **single-sided `Posted`** journal entries with no balance check.
- Trial balance hid correct entries (`Approved` vs `Posted` filter mismatch) and showed
  the broken ones; debits rendered unsigned via `Math.abs`.
- `POST /api/AdminSetup/create-admin` minted administrators with **no `[Authorize]`**.
- Audit logging wrote nothing — the table existed in no migration.
- No tenancy at all: no `tenant_id`, cache keys without tenant, fabricated actor GUIDs.
- Tests were fiction: test SDK inside the production csproj, `Tests/**` excluded from
  compile.

The core lesson baked into everything: *the product has never been simultaneously
buildable and runnable, and nothing was ever checking.* So P0 built "a machine that
cannot lie about state" — `verify-all.sh` plus engineering laws that fail the build.

**Advantage inherited:** the accounting subsystem never ran, so there is **no live
ledger to migrate** — history imports as data, not schema migration.

## Phase map (docs/MASTER_PLAN.md §5)

| Phase | Scope | Status |
|-------|-------|--------|
| P0 | Freeze legacy, truth machine (`verify-all.sh`, law checkers, CI) | ✅ closed 2026-10-16 |
| P1 | Tenancy, identity, RBAC, JWT, RLS, offline superuser | ✅ closed 2026-10-16 |
| P2 | Ledger kernel: CoA/ltree, GL, posting engine, invariants I1–I8, reversal, FX, audit | ✅ kernel landed |
| P2+ | Platform/Data split (law L18), module test suites discovered by gate | ✅ done |
| P3 | Posting sources: manual journal ✅, cash book ✅, bank/reconciliation/CSV import — **in progress** | partial |
| P4 | Reporting projections, verifier, design system applied | design chosen; reports pending |
| P5–P7 | Inventory → Purchasing/Sales → Production/job costing | not started (empty module shells) |
| P7b | Payroll/HR (Tier C, deferrable) | not started |
| P8–P9 | Pilot hardening, onboarding tooling, Bangla marketing assets | not started |

Binding domain decisions (§2.5): BDT functional + full multi-currency · July–June
fiscal calendar · VPS + Docker Compose deployment.

## The spine (how money flows)

```
SourceDocument → PostingIntent → AccountResolver → JournalFactory → Poster
→ Immutable GL → Projections (TB, P&L, BS, Cash Flow, AR/AP aging)
→ PeriodClose → Verifier (control totals, hash chain, drift alarm)
```

A module **never** computes a financial figure from its own tables — it emits a
posting request. Reports are projections re-derivable from GL; denormalised balances
are caches with rebuild + reconciliation only.

## Engineering law summary

18 laws (L1–L18) enforced by `tests/Ostagar.Tests.Architecture`, each proven able to
fail by `LawFirepowerTests`. Highlights: one-type-per-file (L1/L1b), no commented-out
registrations (L2), no fake success (L3), invariants in DB (L4), immutability (L5),
test purity (L6), migrations on empty DB (L7), explicit authorization (L8), tenant
isolation proven (L9), tenant in every cache key (L10), culture-invariant numbers (L11),
one API client (L12), no `Math.abs`/mock data in prod (L13), closed route graph (L14),
design tokens single-source (L15), no dead code (L16), i18n from P0 (L17), persistence
knowledge in one project (L18).
