# MASTER PLAN — Ostagar, greenfield rebuild

**Status:** Approved for execution · **Owner:** Mohsin · **Date:** 2026-10-16
**Legacy tree:** `master` @ `176b9b7`, 108 commits, to be tagged `legacy-final` and frozen read-only.

---

## 0. What we are replacing, and why the plan starts here

Everything in this section was executed and verified on 2026-10-15/16, not inferred from docs.

| Fact | Evidence |
|---|---|
| Backend compiles, but 22 of ~35 service registrations are commented out | [Program.cs:81-112](file:///C:/Users/Mohsin/Projects/garments-software/backend/GarmentsERP.API/Program.cs#L81-L112) |
| Frontend `npm run build` exits **1**; two imports point at files that do not exist | `@/components/icons/SocialIcons` at [login/page.tsx:22](file:///C:/Users/Mohsin/Projects/garments-software/frontend/src/app/login/page.tsx#L22), `@/components/debug/AuthDebug` at [categories/page.tsx:4](file:///C:/Users/Mohsin/Projects/garments-software/frontend/src/app/admin/accounting/categories/page.tsx#L4) |
| Cash book entry controller cannot be constructed — injects two unregistered services | [CashBookEntryController.cs:30-33](file:///C:/Users/Mohsin/Projects/garments-software/backend/GarmentsERP.API/Controllers/CashBookEntryController.cs#L30-L33) |
| `CashBookController` fabricates successful postings | `journalEntryId = Guid.NewGuid()` at [CashBookController.cs:63-64](file:///C:/Users/Mohsin/Projects/garments-software/backend/GarmentsERP.API/Controllers/CashBookController.cs#L63-L64) |
| Cash book posts **single-sided, `Posted`** journal entries — one line, no balance check | `// Create single journal entry line (credit only)` at [EnhancedCashBookService.cs:372-401](file:///C:/Users/Mohsin/Projects/garments-software/backend/GarmentsERP.API/Services/EnhancedCashBookService.cs#L372-L401) |
| The trial balance **hides correct entries and shows the broken ones** | completed entries are set `Approved` [:188](file:///C:/Users/Mohsin/Projects/garments-software/backend/GarmentsERP.API/Services/EnhancedCashBookService.cs#L188); the report filters `Status == Posted` [:299](file:///C:/Users/Mohsin/Projects/garments-software/backend/GarmentsERP.API/Services/TrialBalanceService.cs#L299) |
| Debits arrive **negative** from the API and are rendered **unsigned** in USD | [TrialBalanceService.cs:328-330](file:///C:/Users/Mohsin/Projects/garments-software/backend/GarmentsERP.API/Services/TrialBalanceService.cs#L328-L330) → `Math.abs(amount)` at [TrialBalanceTreeGrid.tsx:28](file:///C:/Users/Mohsin/Projects/garments-software/frontend/src/components/trial-balance/TrialBalanceTreeGrid.tsx#L28) |
| Equity is fabricated as `\|assets\| − \|liabilities\|` and labelled "Net Position" | [TrialBalanceDashboard.tsx:70-80](file:///C:/Users/Mohsin/Projects/garments-software/frontend/src/components/trial-balance/TrialBalanceDashboard.tsx#L70-L80) |
| Trend sparklines are one hardcoded SVG path per row; KPI trends are `{2.5} // Mock trend for now` | [TrialBalanceTreeGrid.tsx:31-41](file:///C:/Users/Mohsin/Projects/garments-software/frontend/src/components/trial-balance/TrialBalanceTreeGrid.tsx#L31-L41), [TrialBalanceKPIs.tsx:169](file:///C:/Users/Mohsin/Projects/garments-software/frontend/src/components/trial-balance/TrialBalanceKPIs.tsx#L169) |
| Audit logging writes nothing, ever — the table exists in no migration and no snapshot | `CreateTable(name: "AuditLogs")` → 0 matches; `AuditLogEntry` → 0 occurrences in `ApplicationDbContextModelSnapshot.cs` |
| Actor identity is fabricated: `return Guid.Parse("00000000-…-000000000001")` with `// TODO: Get from JWT` | [JournalEntryController.cs:365-370](file:///C:/Users/Mohsin/Projects/garments-software/backend/GarmentsERP.API/Controllers/JournalEntryController.cs#L365-L370) |
| **Anyone can mint an administrator.** No `[Authorize]` on the controller or the action | [AdminSetupController.cs:10,30](file:///C:/Users/Mohsin/Projects/garments-software/backend/GarmentsERP.API/Controllers/AdminSetupController.cs#L10) |
| EF model and schema disagree: `date` vs `timestamptz`, enum-as-string vs integer, 12 declared indexes never migrated | [DatabaseConfig.cs:113-115](file:///C:/Users/Mohsin/Projects/garments-software/backend/GarmentsERP.API/Config/DatabaseConfig.cs#L113-L115) vs snapshot `:220` |
| One migration uses a SQL Server type against Postgres — it cannot apply | `type: "uniqueidentifier"` at [20250726200000_AddCategoryIdToJournalEntryLine.cs:16](file:///C:/Users/Mohsin/Projects/garments-software/backend/GarmentsERP.API/Migrations/20250726200000_AddCategoryIdToJournalEntryLine.cs#L16) |
| No tenancy exists. Zero `tenant_id`/`CompanyId` on any accounting table; `TrialBalances` snapshot table keyed by Year/Month | grep across `Models/`, `Data/`, `Migrations/` |
| Cache keys carry no tenant; invalidation is a logging stub; "memoization" keys on `string.GetHashCode()` | [TrialBalanceCacheService.cs:130-174](file:///C:/Users/Mohsin/Projects/garments-software/backend/GarmentsERP.API/Services/TrialBalanceCacheService.cs#L130-L174), [TrialBalanceCalculationMemoizationService.cs:219-236](file:///C:/Users/Mohsin/Projects/garments-software/backend/GarmentsERP.API/Services/TrialBalanceCalculationMemoizationService.cs#L219-L236) |
| Tests are fiction: `<Compile Remove="Tests/**" />` while `xunit`/`Moq`/`Test.Sdk` ship inside the production csproj | [GarmentsERP.API.csproj:13-14,31-39](file:///C:/Users/Mohsin/Projects/garments-software/backend/GarmentsERP.API/GarmentsERP.API.csproj#L13-L14) |
| No CI, no tags, no `.env`, two conflicting `next.config` files, no foreign keys at all | [ApplicationDbContext.cs:391](file:///C:/Users/Mohsin/Projects/garments-software/backend/GarmentsERP.API/Data/ApplicationDbContext.cs#L391) |

**The actual lesson.** This product has never been simultaneously buildable and runnable, and nothing was ever checking. So the rewrite's first deliverable is not a feature — it is a **machine that cannot lie about state**. Every phase below terminates at a gate that a script, not a narrative, decides.

**One advantage we inherit:** because the accounting subsystem never ran, **there is no live ledger to migrate**. We build the new schema clean and import history as data. `docs/progress.md` claiming Purchasing complete is also wrong — `PurchaseOrder`, `SalesOrder`, `WorkOrder`, `BillOfMaterial`, `Payroll`, `Attendance` and `Leave` entities do not exist anywhere. Five of the six modules are genuinely new, not revived.

---

## 1. Product definition

**One sentence:** a multi-tenant, double-entry-first ERP for garments manufacturing and trading businesses, whose books are provably correct and whose screen quality reads as a commercial product.

### 1.1 Production-ready means all of these
1. Every endpoint reachable and correct, or the file is deleted. No dead code shipped.
2. Ledger invariants enforced **in the database**, not in a service that might be commented out.
3. Two tenants provably isolated, verified by an automated cross-tenant test that runs on every commit.
4. `up` migrations apply cleanly to an empty Postgres in CI. Rollback tested.
5. Real, executable test suites on both sides, in the build, gating merge.
6. Separated `dev` / `staging` / `prod` topology, no MailHog or pgAdmin in prod.
7. Backup + **restore drill** evidence, not just a backup cron.
8. No anonymous endpoint except health/readiness and explicitly public auth routes.
9. Observability: structured logs with request correlation, metrics, error tracking.
10. A documented release procedure a competent operator can follow cold.

### 1.2 Marketing-ready means all of these
1. A public landing page built from an approved `DESIGN.md` token set (§4).
2. A **seeded demo tenant** with realistic apparel data — no `Math.abs`, no mock sparklines, no "Mock trend for now".
3. Self-serve trial signup → working workspace in under 3 minutes, unassisted.
4. Screenshots, a product tour and a short demo video captured from the real app, never from mock data.
5. Pricing page, security/data page, docs hub, onboarding checklist.
6. A named reference: the the client cash-book import shown as a live before/after.
7. Zero broken links: a CI route-graph check proves every nav href resolves.

### 1.3 Explicitly in scope for v1 ("everything")
Accounting · Inventory & products · Sales, invoicing & payments · Purchasing & vendor bills · HR & payroll · Production (work orders, BOM, WIP, subcontracting, job costing) · Reporting · Tenancy/admin.
Nothing is parked. Modules land in tranches (§3), because accounting correctness outranks breadth and every one of these modules is a *producer* into the same ledger.

### 1.4 Who actually buys this (owner decision, 2026-10-16)

**Target customer: the owner of a garment factory** — both small/informal and mid-size planned factories, v1. **Go-to-market: one hand-held design-partner pilot, then reference selling.** Self-serve trial and SEO landing page are explicitly **v2 motions**, not launch requirements.

The five things that make a factory owner pay, in the order they feel them:

1. **"Where is my order?"** — status per style, per buyer, against the ship date. The screen they open every morning.
2. **"What did this style really cost me, am I losing money?"** — job costing. Most small factories genuinely don't know; finding out is the trust moment.
3. **Fabric and accessories on hand vs committed**, with wastage and shrinkage.
4. **Cash today** — the daily cash book, who owes me, what's due to suppliers.
5. **Reports their accountant and the VAT office will accept.**

The ledger core is the *foundation* of 2, 4 and 5, so P2 is unchanged — but the ledger is the reason they **stay**, not the reason they **buy**. Production and job costing must therefore not sit last in the sequence (§5 revised below).

**Consequences we accept:**

Module quality tiers, so "done" means something specific at each gate:

| Tier | Modules | Standard |
|---|---|---|
| **A — world-class** | Ledger and accounting core, cash book, reporting, **order progress and job costing** | Invariants proven by tests; the buying reasons live here. |
| **B — correct and complete for an SMB** | Inventory/stock ledger, purchasing, sales/invoicing/payments, VAT-ready reports | Right numbers, no enterprise depth. |
| **C — thin, deferrable** | Payroll and HR | Attendance, piece/CMT rates, payslip. Not enterprise HR. |
| **Out of v1** | APS/finite-capacity scheduling, MRP explosion, QMS, barcode WMS, self-serve billing | Excluded deliberately — see below. |

- **"Both bands at once" is the acknowledged risk in this plan.** Designing for small and mid-size simultaneously is how an SMB ERP becomes too heavy for the small buyer and too shallow for the mid-size one. Two controls:
  - **Excluded from v1:** finite-capacity scheduling / APS, MRP explosion, quality management systems, barcode WMS. A mid-size planning department needs a different product; a progress tracker plus accurate costing wins the buying decision instead.
  - **Hedge: two pilots, one per band.** Since onboarding is hand-held anyway, the second pilot is cheap and reveals which production mode is real rather than averaging two customers into one design.
- **Production ships in two modes over one data model:** simple stage progress (cutting → stitching → packing → dispatch) for the small factory, and BOM-driven issue/consume for the mid-size one. Same tables, same postings; the configuration differs, the costing never does.
- **Language: Bangla is a first-class UI language, not a bolt-on.** See L17 and §4.1 — this is decided at P0, not retrofitted.
- **The demo tenant must be a factory**, with buyers, styles, orders, fabric inventory and a live month — not a generic trading company.

---


### 2.1 The spine: every module posts, nothing reports from its own tables

The previous build had three trial-balance engines that disagreed by construction. The fix is structural: **one immutable ledger, everything else is a projection.**

```
SourceDocument            invoice, bill, payment, stock move, payroll run, work order
   │                      (mutable, tenant-scoped, has its own workflow)
   ▼
PostingIntent             append-only event: doc type + id, event, posting date,
   │                      currency, rule_version.  Idempotency key lives here.
   ▼
AccountResolver           effective-dated account determination → emits a snapshot
   ▼
JournalFactory            builds a balanced line set; header-vs-line rounding decision;
   │                      dual-currency amounts on every line
   ▼
Poster                    ONE transaction: mint gapless number (SELECT … FOR UPDATE),
   │                      insert idempotency row, period-lock check, balance trigger
   ▼
Immutable GL              journal header + lines. UPDATE/DELETE revoked.
   ▼
Projections               Trial Balance · P&L · Balance Sheet · Cash Flow · AR/AP aging
   │                      all keyed by as_of, all re-derivable from GL
   ▼
PeriodClose               the closing journal is an ordinary journal; it advances the lock
   ▼
Verifier                  control totals, hash chain, CoA-vs-posting integrity, drift alarm
```

Consequences we accept for the whole program:
- A module **never** computes a financial figure from its own tables. It emits a `PostingIntent`.
- Report numbers are derived, and any derived number can be rebuilt from GL. Denormalised balance columns are permitted only as caches with a rebuild job and a reconciliation control.
- Historical documents do not recompute when a rule changes. Resolved accounts, rate and rule version are **snapshotted onto the line**.

### 2.2 The eight invariants (researched, then enforced)

Sourced from Odoo `account.move`, ERPNext GL Entry + Period Closing Voucher + Immutable Ledger, NetSuite GL Audit Numbering and period close, Oracle header-level rounding, Xero journal reversal, Square "Books: an immutable double-entry service", TigerBeetle account flags, and Postgres RLS practice. Full list in the research record; the engineering rules:

| # | Rule | Enforcement |
|---|---|---|
| I1 | `Σ debit = Σ credit` per journal **and per currency** | DB trigger re-aggregating siblings in-txn → `RAISE EXCEPTION`. App validation is advisory only. |
| I2 | A line has `debit > 0` **XOR** `credit > 0`, never both, never zero | `CHECK` constraint. No single-signed `amount` column. |
| I3 | Posted ⇒ immutable. Corrections are reversal entries carrying `reversed_entry_id` (+ Storno flag) | `REVOKE UPDATE, DELETE ON gl_* FROM app_role`. Status flags are **not** immutability — the old code's `Reversed` flip left the money in the books. |
| I4 | Gapless monotonic numbering per (tenant, journal, period), minted at post time, never recycled | counter row + `SELECT … FOR UPDATE`; voided documents still consume a number; unique index backs it. |
| I5 | Posting is idempotent on a source key | `UNIQUE (tenant_id, source_type, source_id, iteration)`. Makes safe retry and legal re-posting possible. |
| I6 | Effective-dated period lock, checked at write | Odoo's three tiers: soft lock for non-advisers, hard lock, tax lock. Prior-period adjustments post *into* the open period with disclosure, never retro-posted. |
| I7 | Money typed per currency, one explicit rounding policy | `numeric` with per-currency scale (BDT 2dp), never float. Rounding residue posts to a configured **Header Rounding Account** (Oracle pattern), never truncates silently. Line stores transaction *and* functional amounts. |
| I8 | Limits are DB-side; balances are derived and re-derivable | TigerBeetle-style flags adapted for stock: negative stock, over-allocation, credit limit as constraints, not UI warnings. |

### 2.3 Multi-tenancy
Shared schema, `tenant_id` leading every table, **`FORCE ROW LEVEL SECURITY`**, app role is not the table owner, `set_config('app.tenant_id', …, true)` set per transaction.
Not `AddDbContextPool` — pooling makes the session variable leak between requests. Use `AddDbContext` with a pooled *connection* (`Npgsql` does this itself, and resets state on reuse). Schema-per-tenant is reserved for a future regulated tier, not v1.
Every cache key, every queue message, every background job and every file path carries `tenant_id`. The old cache key (`startDate:endDate:groupBy:…`, no tenant) is the exact bug this closes.

### 2.4 Chart of accounts
Adjacency + `ltree` path, GiST-indexed. Parent rows are **non-postable** (ERPNext rejects GL entries on group accounts). `AccountType` **and** `AccountSubType` are explicit columns — the old code sniffed `accountNameLower.Contains("cash")`, and allowed `AccountType` to be flipped on an account with posted lines, silently inverting every historical balance. Subtypes are immutable once posted to.

### 2.5 Confirmed domain parameters (owner decisions, 2026-10-16 — binding)

These three were resolved because each lands in a table that invariant I3 makes immutable.

1. **Currency: BDT functional, full multi-currency.** Every GL line stores transaction amount *and* BDT functional amount, with the rate and its effective date snapshotted onto the line (I7). Unrealised FX revaluation runs at period close and posts through the ordinary posting engine — not as a manual balance adjustment. Foreign-currency vendor bills are assumed present, which is realistic for garment import of trim and fabric.
2. **Fiscal calendar: July–June, 12 periods** (Bangladesh fiscal year). Gapless numbering (I4) is scoped per `(tenant, journal, period)` on this calendar; the three-tier lock (I6) advances on it; `JournalType.Opening` posts the 1 July opening journal. Period 13 support is *not* built — the schema keeps the period key as a foreign reference so a 13-period tenant can be added without altering the GL table.
3. **Deployment: VPS + Docker Compose.** `infra/compose.prod.yml` is a real topology, not the current dev stack with MailHog/pgAdmin/Redis-Commander bolted on. P8's restore drill is a documented `pg_restore` against a fresh volume with row-count and control-total verification — the `Verifier` totals from P4 are what proves a restore is faithful, not just that it completed.

### 2.6 Solution layout — modular monolith, one type per file

```
src/
  Core/                 money, tenant context, clock, ids, results, errors
  Ledger/              CoA, journals, GL, posting engine, periods, reversal  ← the kernel
  Platform/            tenancy, identity, RBAC, audit, files, email, jobs, cache
  Modules/
    Inventory/         products, apparel matrix, warehouses, stock ledger
    Purchasing/        PO, GRN, vendor bills, payments
    Sales/             SO, invoices, receipts, allocations, tax
    Payroll/           employees, attendance, payslips, payroll journals
    Production/        work orders, BOM, WIP, subcontracting, job costing
  Api/                 composition root, controllers, middleware  (the only host)
tests/
  Unit/  Integration/  LedgerProperty/  LedgerGolden/  TenantIsolation/  E2E/
web/                   Next.js 16 app
design/                DESIGN.md token source, component specs
infra/                 compose.dev.yml  compose.prod.yml  ci/  observability/
```

### 2.7 Layering: entities and persistence were fused in `Platform` (found and split in P2)

**Diagnosis, corrected on inspection.** `Platform` currently holds two things that must not be in the
same project: the **domain entities** (`Tenant`, `CompanyProfile`, `Department`, `ResourceAction`,
`RoleGrant`, `AuditLogEntry`, `ApplicationUser`, `ApplicationRole`) and the **persistence layer** that
maps them (`OstagarDbContext`, migrations, `TenantIdConverter`, `RlsSafetyProbe`, `MigrationRunner`),
plus services that use that context directly (`PermissionSeeder`, `TenantDirectory`).

The dependency runs **both** ways, verified by grep rather than assumed:

```
Platform/Rbac/PermissionSeeder  →  Platform/Data/OstagarDbContext   (needs the context)
Platform/Data/OstagarDbContext →  Platform/Tenancy/Tenant, Rbac/…   (maps the entities)
```

So creating a `Data` project alone does **not** break the cycle — `Data` would need `Platform` for the
entities while `Platform`'s services needed `Data` for the context. This is why the ledger entities in
`src/Ledger` are built, tested and deliberately **not yet registered**: registering them requires this
to be resolved first.

**Target layering.** Split along the seam that already exists in `Ledger`, where entities and rules
live together and nothing about persistence is known:

```
Core      primitives + ITenantScoped (moved down from Platform.Tenancy)
Platform  domain entities and pure services: tenancy context, RBAC model, audit model,
          identity users, token issuing, current-user reads.  NO DbContext reference.
Ledger    ledger entities and rules                            → Core
Data      OstagarDbContext, migrations, design-time factory, TenantEntityConfiguration,
          SnakeCaseModelNames, TenantIdConverter, TenantOwnedTables, OstagarDatabaseOptions,
          RlsSafetyProbe, MigrationRunner, and the two context-using services
          (TenantDirectory, PermissionSeeder)                   → Core, Platform, Ledger
Api / Cli composition root, controllers, middleware, DI wiring  → Data, Platform, Ledger
```

Acyclic: `Data` depends on the entity projects; nothing depends on `Data` except the hosts.

**Why the services move rather than being abstracted behind interfaces.** A repository or
`IDbContext`-shaped interface would break the cycle too, but it buys indirection with no seam we
actually want: there is one context, one database, and the isolation guarantee depends on that
concrete context owning the tenant query filters. Hiding it behind an interface makes the tenancy
rules easier to bypass, which is the opposite of this project's direction.

**Closed in P2.** The split ran as recorded: `git mv` first, then namespaces in one pass, then build.
`src/Data/Ostagar.Data.csproj` now holds both contexts, the migrations, the two tenant interceptors,
`TenantDirectory`, `PermissionSeeder` and `LedgerModelConfiguration`; `Platform` compiles with no EF
package at all, and `Ledger` references only `Core`. `ITenantScoped` moved to `Core` — the marker has to
sit below every project that implements it, and putting it beside the entities is what forced the merge.

Two things the plan did not predict:

- Relative namespace names broke, not the usings. Code written as `new Tenancy.AmbientTenantContext()`
  resolved because the file sat in `Ostagar.Platform.Data` and `Tenancy` was a sibling. From
  `Ostagar.Data` it is not, so eleven call sites needed `Platform.` qualification. The usings, which
  is what the plan warned about, needed only five edits.
- `Platform` still compiles without `Microsoft.AspNetCore.Identity.EntityFrameworkCore`, because
  `IdentityUser`, `IdentityRole` and `UserManager` are all in the ASP.NET Core shared framework. Only
  the *stores* are EF-specific, and they were the thing moving anyway.

**Kept closed by L18.** A law rather than a review note, because the fusion is invisible in any single
file: one navigation attribute on a domain entity starts the cycle, and by the time it shows up in a
diff the way out is a refactor of both sides. L18 fails any domain project that references
`Ostagar.Data`, takes an EF package, or writes an EF `using` — and its firepower probe plants exactly
that, so the law is known to be able to fail.

**Rejected alternatives, recorded so they are not re-litigated.**
- *Move the context into `Api`*: unusable by unit tests and drags the host into the data layer.
- *Move ledger entities into `Platform`*: puts the kernel inside infrastructure.
- *A model-contributor callback injected into the context*: hides which entities are mapped, and a
  silently unmapped entity is a table that never gets created.
- *Leave it and register the ledger from `Platform` anyway*: compiles only by deleting the cycle's
  evidence, and every later module inherits the tangle.

---

---

## 3. Engineering law — enforced by CI, not by memory

The failure mode of the previous build was not ignorance, it was **no gate**. Each rule below has a checker; a rule with no checker is a preference.

| # | Rule | Gate |
|---|---|---|| L1 | **One type per file.** Every class, interface, enum, record and DTO — C#, TypeScript, everywhere. Two enums sharing a file breaches it exactly as two classes do, and a public nested type is only a workaround. | Roslyn rule: >1 top-level type per file, or any nested enum/visible type, fails the build. |
| L1b | **File name equals the type it declares.** No aggregate files — no `JournalEntryDtos.cs`, no `TrustSignals.cs` holding an enum plus a record. The plural-category filename is the tell that a drawer of types got parked in one file. | Roslyn rule: file stem must match its single top-level type; a fixed list of aggregate stems (`*Dtos`, `*Models`, `*Types`, `*Enums`, `*Common`, `*Helpers`, …) fails outright. |
| L2 | No commented-out registrations, ever. Composition root resolves **every** controller and service. | Startup test enumerates all controllers/`I*Service` and constructs each. Missing DI = red. |
| L3 | No fake success. Ban `Guid.NewGuid()` as a returned identifier, `NotImplementedException` behind a 200, and `TODO` in any controller path. | CI grep + "did this actually persist?" integration assertion per write endpoint. |
| L4 | Financial invariants live in the database (I1-I8). App validation improves the error message, nothing more. | Migration diff review + constraint-existence test. |
| L5 | Posted rows immutable. Corrections are reversals with a link. | `REVOKE` present in migration; test asserts `UPDATE` on posted GL throws. |
| L6 | Test packages never reference the app project's production csproj. Tests compile in the build. | Separate test assemblies; CI fails on test SDK in `Api.csproj`. |
| L7 | Migrations apply to an **empty** Postgres in CI, forward and back. | Catches the `uniqueidentifier` class of failure that hid for a year. |
| L8 | Every endpoint has explicit authorisation. `FallbackPolicy = RequireAuthenticatedUser`; opt out per-route, never opt in. | Metadata scan: any endpoint without a policy is red. Anonymous allow-list is a reviewed file. |
| L9 | Cross-tenant access is impossible and *proven*. | `TenantIsolation` suite: tenant A's principal requesting every B resource by guessed id, all endpoints. Must fail. |
| L10 | Every cache key contains tenant. Every cache has a tested invalidation path. | Cache-miss/invalidation test per cached read. |
| L11 | Culture-invariant for all persisted/serialised numbers. | Roslyn analyzer for `ToString()`/`(decimal)` casts on money. |
| L12 | Frontend: **one** env-driven API client, one auth interceptor. No raw `fetch` + `localStorage`. | ESLint `no-restricted-imports` + CI grep for `fetch(` and `localStorage` outside the client. |
| L13 | No `Math.abs` on a financial value in a render path. No hardcoded currency or locale. No mock data reachable from a production build. | CI grep; `NODE_ENV`-branched mock endpoints are banned outright (that is how `dev/trial-balance` fooled everyone). |
| L14 | Route graph must be closed: every nav href resolves; every page has an inbound route. | Script walks `src/app` against nav config. |
| L15 | Design tokens have one source. No inline hex/`rgba` literals in components (~40 in the old trial balance alone). | Stylelint/ESLint rule; token file is the only place colours exist. |
| L16 | No dead code ships. Unreachable components are deleted in the phase that replaces them. | Bundle/Knit coverage + explicit "components reachable" report per gate. |
| L17 | **Every user-facing string goes through a translation layer from P0.** The buyer is a Bangladeshi factory owner; the operating language is Bangla, with English retained for reports and account codes. Retrofitting i18n into a finished app is the expensive version of this. | ESLint rule failing any hardcoded JSX string; CI asserts no untranslated keys in the shipped bundle. Postgres collation and number/date formatting verified with Bangla data at the P4 gate, not discovered later. |
| L18 | **Persistence knowledge lives in one project.** No domain project may reference `Ostagar.Data`, take an Entity Framework package, or write an EF `using`. Hosts (`Api`, `Cli`) are exempt because a composition root must register a context; a domain project has no such excuse. Found in P2 as §2.7. | csproj scan plus a source scan of `src/`, with the §2.7 layering as its exemption list. The probe plants a domain file holding a `DbSet` property, so the law is known to fire — the failure mode it prevents is invisible in any single file. |

**Every law must be shown to be capable of failing.** The legacy repository's checks had never been observed to reject anything — a test folder excluded from compilation, a validator never registered, a cache invalidator that logged and returned — which makes a rule that does nothing indistinguishable from a rule that is working. So each law ships with two tests: that it holds on the real tree, and that it fires on a planted known-bad sample. `LawFirepowerTests` writes deliberately non-compliant files under `src/`, re-runs every law, and asserts each one reacts, then cleans up. A law added without a firepower probe is not a law.

This requirement already paid for itself. The first firepower run failed all six probes — not because the breaches were hard to spot, but because `SourceTree` cached its file list on first access, blinding every law to any file created afterwards. A check that cannot see a new file cannot see a newly introduced breach either, and it had been passing happily.

---

## 4. Design — routed through the tooling, per standing instruction

Design is not freehand. Per the owner's rule, every interface task goes through **`impeccable`** (design, critique, audit, polish) and **`awesome-design-md`** (exact token sets from named brands).

**Direction:** a financial product that reads trustworthy, not playful. Dense, tabular, numeric-alignment-first. Candidates to evaluate at the P4 gate, then one is chosen and written to `design/DESIGN.md` as the single token source: **Linear** (restraint, density), **Stripe** (financial credibility, type discipline), **Notion** (calm surfaces). Rejected by default: decorative hover lifts on data cards, gradient hero cards, sparklines without a real series.

**The accounting UI patterns we hold ourselves to** — each responds to a specific verified defect:
- Signed amounts, never `Math.abs`. Dr/Cr shown as signed with explicit column semantics; negative in red **with the sign present**.
- Real account codes in the "Code" column. The old grid printed a truncated GUID under a header promising a code.
- Per-currency normal-balance logic surfaced to the client (`accountType`/`subType` must survive the DTO boundary — the old `AccountBalanceDto` dropped `categoryName`).
- Ledger-correct date range with an explicit "Opening / Period movements / Closing" column set, and the range actually changeable (the shipped screen imported `DateRangeSelector` and never mounted it, so it was permanently month-to-date).
- Every table: sticky header, `TableContainer` scroll, breakpoint variants, skeleton, empty state, **and a real error state**. `catch { console.error }` rendering a blank page under a live header is banned; so is the worse variant — a plausible-looking empty report with no message.
- No trend, chart or KPI without a real series behind it. Two screens must not disagree on currency (old: trial balance hardcoded `USD`, balance screen showed `BDT`).
- One theme. The old `/trial-balance` forced `#0B1437` navy against a `#ffffff` sidebar from the shared shell.

`impeccable` runs as a **critique gate** at the end of each UI-bearing phase; its findings are fixed before the phase is called done, and the before/after is captured for the marketing assets in P9.

### 4.1 Bangla typography and numeric display — decided at P0, not retrofitted

The buyer is a Bangladeshi factory owner, so the operating language is Bangla while money, account codes and formal reports stay in English/Latin. Requirements:

- A Latin type pairing **and** a Bengali script family with matching metrics, so mixed labels like `স্টক / Stock` don't break baseline alignment. `awesome-design-md` gives Latin tokens; the Bengali pairing is chosen and tested against real strings at the P4 gate.
- Tabular figures for all money columns — proportional digits make a ledger column unreadable.
- Amount formatting: BDT with thousand-grouping agreed per tenant (Western `1,234,567` vs South-Asian lakh-style), and the choice stored as tenant settings, not inferred from browser locale — the legacy screen hardcoded `en-US`/`USD`.
- Postgres collation verified with Bangla account names and particulars for sorting and search, and search must work on both scripts (a clerk typing "Cotton" and one typing `সূতি` should reach the same row).
- Translation layer (L17) with the string inventory extracted before P1, because retrofitting i18n after 30K lines of JSX is the expensive path.

---

## 5. Phases and gates

Every phase ends at a **gate**: a command that prints PASS/FAIL with evidence, plus a live screen or endpoint I demonstrate. Nothing starts until the previous gate passes. Gates are reviewed by you.

### P0 — Freeze and the truth machine
Tag `legacy-final`; extract read-only schema + the client CSV mapping into `legacy-reference/`. New solution skeleton at §2.5 with an empty bootable app. Build CI: both builds, both lint, type-check, migrations-on-empty-DB, L1/L2/L3/L6/L14/L15 checkers, and a `verify-all.sh` that is the single gate command. Fix the frontend's two dead imports only if we keep any page; otherwise the new `web/` starts clean. Docker Desktop must be running — it currently is not.
**Gate:** green pipeline on an app that does nothing yet, on every push. *This is the phase that could not have been skipped.*

**✅ P0 CLOSED 2026-10-16 — `verify-all.sh` PASS, 11 checks.** Frontend: 105 tests, lint clean, production build succeeds, route graph closed (3 pages, 8/8 components reachable). Backend: 13 law tests (6 hold, 6 fire on planted breaches, 1 stays silent on compliant prose). Coverage line correctly reads `controllers: 0` — the harness is real, the product is not built yet. Every law was proven to fire by planting violations and then removing them.

#### P0 execution record (2026-10-16)

Frozen and restructured: `legacy-final` → `adbc880`. `backend/`, `frontend/`, `nginx/`, `pgadmin/`, `scripts/`, `ssl/` and the old compose/Makefile/solution moved under `legacy/` with `git mv`, so the read-only reference is the whole tree rather than a hand-picked extract; `legacy/Directory.Build.props` severs root build inheritance so the frozen code still builds as it was tagged. New tree: `Ostagar.slnx` with `src/{Core,Platform,Ledger,Api,Modules/{Inventory,Purchasing,Sales,Payroll,Production}}` and `tests/{Unit,Architecture,Integration}`. Docker Desktop was started and is running.

The law checkers are code, not prose — L1, L1b, L2, L3, L6 and L8 live in `tests/Ostagar.Tests.Architecture` and fail the build. `Directory.Packages.props` pins the whole tree to `net10.0` + EF Core 10.0.13 + Npgsql 10.0.3, ending the mixed 8.0.x split that caused the legacy damage. `verify-all.sh` is the single gate command and CI runs the same steps.

Three things this phase surfaced that are worth keeping:

1. **A green gate over an empty set is not coverage.** The composition-root test passed while matching zero controllers. `verify-all.sh` now prints gate coverage counts — controllers, endpoints, migrations, DB constraints, source and test files — on every run, so an empty-but-green report reads as empty instead of as done. It also gained a boot step, because building is not starting: "does the process come up and answer `/health`" is the cheap check that would have caught 22 commented-out registrations years ago.
2. **Every law is now proven capable of failing, and that found a bug in the harness itself.** `LawFirepowerTests` plants known-bad files and asserts each law reacts. Its first run failed all six probes — not from subtle detectors but because `SourceTree` cached its file list on first access, so every law was blind to any file created after that moment. A checker that cannot see a new file cannot see a newly introduced breach. The suite is 12 tests: 6 laws holding on the real tree, 6 firing on planted samples, with cleanup verified to leave no residue.
3. **The one-type-per-file rule needed a second rule to be worth anything.** Standing alone it is routed around: two enums sharing a file already violated it, and a public nested type or an `*Dtos.cs` drawer would too. Added **L1b** (file name must equal the single top-level type it declares; aggregate stems like `*Dtos`, `*Models`, `*Types` fail outright) and extended L1 to reject nested enums and visible nested types. Both are Roslyn-based, and both are covered by firepower probes.
4. **Environment traps, verified by real binds rather than by reading an exclusion list.** `netsh … excludedportrange` does not clearly report them, so every port below was chosen by opening a listener and watching it succeed or fail: **5199, 5434 and 5435 fail with `EACCES`** (Hyper-V reserves the whole 5432-5435 band), and **6379 and 3000 are already published by another project's `credivect` stack**. An ASP.NET app dying on `SocketException 10013 … forbidden by access permissions` is this, not a bug — which is exactly how it looked at first. Chosen and confirmed free: **Postgres 6432, Redis 6380, API 8080, Next dev 3100, alt 7123.** Also: `docker compose` resolves `.env` against the compose file's own directory, so `infra/compose.dev.yml` needs an explicit `--env-file .env` (now in the Makefile) or `${POSTGRES_PASSWORD:?…}` fails as "missing". The dev stack is up and healthy on those ports. Related product bug found while testing: HTTPS redirection must exempt `/health`, or a load balancer probing plain HTTP gets a 307 — handled with `UseWhen` in `Program.cs`.
5. **`§4.1`'s Bangla collation assumption is proven, not assumed.** Postgres 17 alpine ships `bn-x-icu`, `bn-BD-x-icu` and `bn-IN-x-icu`, and `ORDER BY … COLLATE "bn-x-icu"` sorts ক, কা, খ, গ in correct varnamala order. A `--locale=C` initdb therefore does not cost proper Bangla sorting; it is declared per column instead.
6. **Two inherited defects corrected in passing**, both of which made the legacy tree unshippable: the root `.gitignore` ignored `package-lock.json`, `yarn.lock` and `pnpm-lock.yaml`, so no frontend build was ever reproducible (lockfiles are committed now); and `.env.example` carried pgAdmin and Redis-Commander credentials for tooling that must not exist in a production topology at all.

### P1 — Tenancy, identity, RBAC
`Tenant`/`Company`, `ApplicationUser`, profiles, roles, `(resource, action)` permissions **seeded** (the old seeder was commented out, so the permission layer was decorative). RLS forced, `set_config` per transaction, JWT with real subject claims — one claim name, not the old three (`NameIdentifier` / `sub` / `id`). Deny-by-default fallback policy. Superuser bootstrap as an offline CLI, never an HTTP endpoint like `POST /api/AdminSetup/create-admin`.
**Gate:** `TenantIsolation` suite passes; two seeded tenants cannot see each other through any endpoint.

**✅ P1 CLOSED 2026-10-16 — gate green at 12 checks.** Tenancy, RLS, identity, RBAC, JWT, deny-by-default, isolation proven through real HTTP, and offline superuser provisioning. Three defects the gate caught that would otherwise have shipped:

1. **Identity's global UNIQUE indexes.** `roles.NormalizedName` and `users.NormalizedUserName` were unique across the whole table, so the second tenant to create an "Owner" role was rejected outright — and the collision itself confirms to tenant A that a name exists somewhere else in the system. The users block already carried a comment describing this exact hazard and had not fixed it.
2. **JwtBearer rewrites inbound claim types.** `sub` arrived as `…/claims/nameidentifier` and `tid` as `…/claims/tenantid`, so every reader keyed on `AuthClaimNames` silently found nothing. There was no error — the caller authenticated, the tenant resolved empty, and every tenant-scoped request was refused. `AuthClaimNames` already *promised* wire names are not remapped; `MapInboundClaims = false` is what makes the promise true. A comment asserting a property the code does not enforce is the most expensive kind of documentation.
3. **A test that asserted the wrong thing while appearing to pass.** The tampered-token case string-replaced `"tid"` inside base64url, producing a token for a *nonexistent* tenant — which correctly returns an empty list. It now mints a genuinely tenant-less token signed with the real key.

Also: the resolution middleware conflated *resolving* the tenant with *enforcing* it, which made the one endpoint meant to report resolution unreachable behind its own 403. `/api/v1/auth/me` now exists as both that diagnostic and something the client needs anyway.

A **system scope** proved unavoidable and is deliberately narrow: the permission catalogue is a global table with no `tenant_id`, so the fail-closed interceptor rightly refused to seed it. Rather than weaken the guard, maintenance declares itself via `TenantScope.OpenSystem` — greppable, and row-level security still returns nothing if anyone reads tenant data through it.

### P2 — Ledger kernel *(the highest-value phase)*
CoA with `ltree` + non-postable parents; journals; `gl_entry`/`gl_line` with I1-I8 as DB constraints/triggers; gapless numbering; idempotent posting; effective-dated exchange rates; fiscal year, periods, three-tier lock; opening-balance journals (`JournalType.Opening` existed in the old enum and was referenced nowhere); reversal with `reversed_entry_id` + Storno; header/line rounding with a rounding account; tamper-evident append-only audit with before/after values and real actor identity.
**Gate:** property-based tests (FsCheck) over random line sets proving you cannot create an unbalanced, single-sided, double-sided or zero-amount posted entry; `UPDATE`/`DELETE` on posted GL throws; golden-file trial balance correct **including opening balances and prior periods** — the exact thing the old report got wrong.

**Status after the kernel landed.** Done and proven: the fiscal calendar, the balancer with no tolerance, the schema with 14 CHECKs and `ltree`, the I1/I3/I6 triggers, gapless numbering, idempotent posting, three-tier period lock, reversal with its link and its once-only rule, tenant provisioning, opening balances, effective-dated exchange rates, and a posting trail that is both attributed and append-only. The FsCheck half of the gate is green as an *equivalence* (the balancer accepts exactly when two or more lines balance), and 72 integration tests write genuinely invalid postings against the migrated schema and assert which invariant refused them.

**How a foreign-currency line is priced.** The caller states both amounts and the ledger decides which rate applies and whether the two agree. Deriving the functional amount inside the poster instead would quietly discard a number the caller typed deliberately, which is the kind of helpfulness that leaves a document and its posting disagreeing with nobody able to say who decided it. The rate is resolved from history when the caller leaves it out, and where one is supplied it is honoured — but `amount_in_currency × rate` must equal the taka figure, or the posting is refused naming both. A difference that is recorded nowhere is what invariant I7 exists to prevent.

A recurring defect this phase produced twice, in two different methods of the same test harness: returning a task from a scope-opening method disposes the context while the query is still running, and the driver reports it as "BindComplete while expecting ReadyForQueryMessage", naming neither the scope nor the mistake. The second occurrence is why the rule is written on the harness rather than only fixed at the site.

Still open in this phase, in the order that unblocks the next one:
- **A `before/after` payload on the audit trail.** The posting trail records what was attempted and by whom; it does not yet store the row values either side of the change. Reaching "before/after values" needs a helper beside `IAuditWriter` and every write path passing through it, which is most cheaply done once P3 adds the other sources rather than twice.
- **Golden-file trial balance.** Needs the projection, which is P4's first deliverable. Deferred here on purpose rather than built twice: a report written before the read model exists is a report that gets rewritten.
- **A native Bangla review of the base chart of accounts and journal names.** Written now so law L17 is true from the first screen, but bookkeeping terms have conventional wording a non-native speaker gets subtly wrong, and a subtly wrong account name on an owner's screen is worse than an English one they can read. Scheduled with the P9 Bangla assets.

**Storno is deferred, and it is a schema decision rather than a feature flag.** `UsesStorno` is carried on the journal and nothing honours it. A Storno correction repeats the original side with a *negated* amount, which `CK_gl_line_debit_xor_credit` refuses outright — it requires `> 0`. Turning that into `<> 0` weakens invariant I2 everywhere, including for every path that is not Storno, and a debit that can be negative is a debit whose meaning depends on reading another column first. The non-Storno mirror implemented here is what Bangladesh's factories and their accountants actually expect. Recorded so it is not re-litigated, and to be revisited only if a pilot asks in writing.

### P3 — Posting sources: manual + cash book + bank
Manual journal (the only working path today — `IJournalEntryService`, registered), cash book, customer/vendor payments and allocations, bank accounts, transfers, reconciliation. the client CSV import rebuilt on the posting engine with explicit account-mapping configuration, replacing the old `GetOrCreateCategoryAsync` behaviour of auto-creating CoA accounts from free text with `new Random().Next(100,999)` codes.
**Gate:** real client CSV → balanced GL → trial balance that ties; each write endpoint proven by "did it persist" assertions (L3).

### P4 — Reporting, design system, and the trial balance redo
Projections over materialised read models keyed by `as_of`, all re-derivable: Trial Balance, GL detail, journal listing, P&L, Balance Sheet, Cash Flow, Aged AR/AP, tax return, budget variance, cost-centre/dimension P&L. `Verifier` runs control totals (`Σ gl_lines = reported balance`) and alarms on drift. **`design/DESIGN.md` chosen and applied here** via `awesome-design-md`; `impeccable` critique pass. The 5,611-line `components/trial-balance/` folder — where 15 of 18 components were unreachable and the routed screen was the *worse* of the two — is not ported; the new report replaces it and the dead folder is deleted.
**Gate:** numbers tie to the `Verifier`; export + print; L13/L14/L15/L16 green; screenshot pack exists and every figure on it is real.

### P5 — Inventory & apparel matrix
Products with style/colour/size matrix that must **not** fragment the GL; warehouses, locations; stock ledger as its own append-only ledger reconciling to the GL stock account (ERPNext's stock-ledger-vs-GL comparison is the model); perpetual valuation with backdated-write repost handling; negative stock blocked DB-side (I8); batch/lot and fabric-roll tracking with shrinkage.
**Gate:** `Stock Balance` report reconciles to the GL inventory account to the paisa, on a dataset with backdated movements.

### P6 — Purchasing and Sales cycles
PO → GRN → vendor bill → payment; SO → dispatch/delivery → invoice → receipt → allocation. Tax schemes/rates effective-dated, price lists, partials and credit notes as **reversals** (I3), document workflows with approval. This finally builds the modules `docs/progress.md` marked complete.
**Gate:** procure-to-pay and order-to-cash cycles post correct balanced journals; AR/AP aging ties; credit note leaves both original and reversal visible.

### P7 — Production and job costing *(Tier A — this is what closes the sale)*
Work orders, **two modes over one data model**: simple stage progress (cutting → stitching → packing → dispatch) for the small factory, and BOM-driven issue/consume for the mid-size one. BOM cost roll-up, **WIP as a real GL account fed by consumption**, subcontracting (issued-vs-held stock, processing bills), **job/order costing per style**, COGS. CMT labour at SAM/minute rates, size-colour matrix costing. Order progress per buyer against ship date — the morning screen.
**Excluded here on purpose:** finite-capacity scheduling/APS, MRP explosion, QMS, barcode WMS (§1.4).
**Gate:** a complete job — fabric issued, cutting/stitching/packing consumed, subcontracted trim, FG receipt — produces WIP and COGS that tie to the ledger **and** to the stock ledger simultaneously; and a "cost per style vs price" report the owner can read without an accountant.

### P7b — Payroll and HR *(Tier C, deferrable)*
Employees, attendance, leave, piece/CMT wage calculation, payslips posting through effective-dated determination rules. Deliberately **not** enterprise HR. If the pilot says wages are handled on paper, this slips and the ledger still accepts a manual payroll journal.
**Gate:** a payroll run posts balanced journals and a payslip prints.

### P8 — Pilot hardening
Replaces the old self-serve plan. **Data onboarding tooling is the launch-critical feature**, because a hand-held pilot lives or dies on it: import chart of accounts and opening balances (1 July, per §2.5), buyers/suppliers, styles and size/colour matrices, fabric inventory, open orders, and the cash book history. Plus tenant provisioning from an admin console (no public signup), role setup for owner/manager/accounts/stock-clerk, **printable reports for a paper-first culture**, WhatsApp and email notifications on order status and payment due, VAT/discovery-compliant formats, resilience for flaky connections, and the unglamorous parts: error tracking, structured logs with correlation, backup **and a tested restore drill**, release runbook. Self-serve trial, email verification and billing hooks move to v2.
**Gate:** a real factory's data loads end-to-end and they can answer "where is order X" and "what did style Y cost" unassisted; restore drill is byte-verified with the `Verifier` control totals; prod compose contains no MailHog/pgAdmin/Redis-Commander.

### P9 — Pilot and reference-selling assets
Built for how this buyer actually decides: a 6-minute walkthrough **in Bangla, on the pilot factory's own numbers**, a printable one-page A4 sheet, pricing in BDT, a go-live pack, and a referral mechanic — the second sale comes from the factory owner down the road, not from search. A minimal public page exists so the product can be found, but it is not the conversion engine and no asset may derive from mock data (L13).
**Gate:** pilot one live with real data, pilot two (the other size band) onboarded from the go-live pack without heroics, walkthrough and one-pager reviewed by a factory owner who was not involved in building it.

**Dependency shape:** P0 → P1 → P2 → P3 → P4 → P5 → P6 → P7, with P7b optional and P8 needing P4 demoable. Stock (P5) precedes the cycles that move it (P6), which precede production consumption (P7). P9 needs P8. Two pilots are scheduled at P9 — one per factory band.

---

## 6. Data strategy

No legacy runtime data exists, so this is an import, not a migration.
1. `legacy-reference/` holds the old schema and the the client CSV mapping (read-only).
2. The pilot client's CSVs become the **demo tenant** seed and the P3 tie-out corpus. They were scrubbed from git history on 2026-10-15 and stay out of the repo — referenced by path, never committed.
3. Import is idempotent per I5, and re-runnable after a mapping fix without duplicate postings.
4. Chart of accounts is built once as a **template** with a garments-specific seed, then per-tenant customisable. "the client categories" become tenant data under a configurable category set — the single largest productisation step, because today they are hard-coded into the domain model.

---

## 7. Standing risks and how the plan answers them

| Risk | Answer |
|---|---|
| "Everything" breadth outruns the core and we rebuild a wide-but-dead app. | P2 must gate green before any module phase. Modules are posting sources, never parallel implementations of reporting. |
| Docs drift back into fiction (`progress.md` has one ~120-line block duplicated six times and claims modules that do not exist). | Status lives in the gate script's last output, not prose. `docs/progress.md` is regenerated from evidence in P0 and deleted as a hand-maintained file. |
| Another "temporarily commented out to resolve compilation errors". That phrase is what killed 22 registrations — and it points at the real cause: `net10.0` pinned against `Microsoft.AspNetCore.*` / EF `8.0.x` packages. | Single version set, `dotnet out of support` check in CI, packages upgraded in P0 and locked via `packages.lock.json`. |
| Correctness verified by eyeball. | Golden-file + property-based tests in P2. `Verifier` control totals in P4 run continuously, not on demand. |
| Design regressing to inline hex soup. | L15 gate + `impeccable` critique at every UI phase. |
| Marketing assets built on mock data, as `DevTrialBalanceController` did. | L13 bans mock endpoints in production builds; P9 assets derive only from the live pilot tenant. |
| **"Both bands" drifts into a product too heavy for small factories and too shallow for mid-size ones.** | Tier table §1.4, hard exclusions (APS/MRP/QMS/WMS out of v1), two pilots one band each at the P9 gate, and production shipping as two modes over one data model so the small factory isn't forced to configure a planning department. |
| Bangla support discovered at P9 instead of P0, forcing a string-layer retrofit through the whole frontend. | L17 makes translation-layer-first coding a build-law with an ESLint gate; §4.1 fixes the typography and collation work into P1 and P4. |
| ~~Docker Desktop down, so no phase is verifiable locally.~~ | Resolved in P0: engine started and running. Dev port 5199 is Windows-reserved; use 7123. |

---

## 8. Open decisions

**Resolved 2026-10-16** and now binding in §2.5: currency scope (BDT functional + full multi-currency) · fiscal calendar (July–June, 12 periods) · deployment target (VPS + Docker Compose). Also resolved in §1.4: buyer is the garment **factory owner**, target is **both size bands**, go-to-market is a **hand-held pilot then reference selling**, and payroll/HR drops to Tier C.

Still open, none of them blocking P0:

1. **Tax regime — blocking before P6.** VAT and discovery rules for garments in Bangladesh, and whether withholding tax on vendor bills is in v1. This changes account determination, so it can be late for P7 but not for P6.
2. **Which brand aesthetic** to evaluate at the P4 gate (§4) before `design/DESIGN.md` is frozen — Linear, Stripe or Notion.
3. **Bangla string inventory** — which screens are Bangla-first versus English-first. Needed at P4, not P0, but the L17 translation layer must exist from P0 regardless.
4. **Pilot factory identity.** Two named pilots, one small and one mid-size (§1.4), are the cheapest control on the "both bands" risk. Needed before P8.
5. **v2 billing/entitlement model** — moot until self-serve arrives; deferred deliberately with the rest of the SaaS motion.

---

## 9. Immediate next action

**P0 — Freeze and truth machine.** Not blocked by any open decision. Docker Desktop was started on 2026-10-16 and is running, so the empty-database gate is verifiable. **Dev port 5199 is reserved by Windows on this machine — use 7123.**
