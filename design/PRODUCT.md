# Product

<!-- impeccable:product-schema 1 -->

## Platform

web

## Stack

Not greenfield: Next.js 16 App Router + React 19 + MUI 7, strict TypeScript, already scaffolded at
`web/` with a token-only theme (`web/src/theme/Tokens.ts`), an i18n layer (`web/src/i18n/`), an
env-driven API client (`web/src/api/`) and twelve custom ESLint law rules (`web/rules/`). The
existing stack is binding; this product record does not propose another.

## Users

**Primary: the owner of a Bangladeshi garment factory**, small/informal (Tier A band) and mid-size
with a planning desk (Tier B band). Both bands are targeted at once — this is a named risk in
`docs/MASTER_PLAN.md` §1.4, hedged by two pilots, one per band.

The owner is not an accountant and does not keep books himself. He employs an accountant and a
cash-book clerk; he reads screens himself, mostly in Bangla, mostly in the morning, and he judges
software by whether the number on it matches the number in his head and on his paper.

Secondary users, each with their own role and their own reason to distrust the screen:

- **Accounts clerk / accountant** — posts journals and the cash book, answers the VAT officer and
  the external auditor. Needs the ledger detail, not a summary.
- **Stock clerk** — fabric and accessories on hand versus committed.
- **Production manager** — stage progress per style.
- **Owner's accountant from outside the factory** — reads a printed report and forms an opinion of
  the software without ever seeing it run.

`web/` role names are the shipped set (`src/Platform/Rbac/DefaultRoleMatrix.cs`): Owner, Manager,
Accounts, Stock, Production.

## Product Purpose

A multi-tenant, double-entry-first ERP for garment manufacturing and trading, whose books are
provably correct and whose screen quality reads as a commercial product. It replaces a legacy
build that had never been simultaneously buildable and runnable, and whose trial balance reported
wrong numbers in a hardcoded currency (`docs/MASTER_PLAN.md` §0).

Success for the buyer, in the order he feels it (`MASTER_PLAN.md` §1.4):

1. "Where is my order?" — status per style, per buyer, against the ship date.
2. "What did this style really cost me, am I losing money?" — job costing.
3. Fabric and accessories on hand versus committed, with wastage.
4. Cash today — the daily cash book, who owes me, what is due to suppliers.
5. Reports his accountant and the VAT office will accept.

Success for the sale: he signs after a hand-held pilot in which his own data loads and he can
answer "where is order X" and "what did style Y cost" unassisted. Self-serve trial and SEO are v2
motions and are not this product's launch requirement.

## Positioning

The mechanism a neighbour cannot copy-paste: **one immutable ledger, and every report is a
projection of it.** No module computes a financial figure from its own tables; each emits a
posting intent, and any derived number can be rebuilt from the general ledger
(`MASTER_PLAN.md` §2.1). Eight invariants are enforced in the database, not in a service that
might be commented out (§2.2). A separate `Verifier` runs control totals and alarms on drift, so
the product can state — and show — that the trial balance ties to the ledger.

Against the incumbent in this market (a paper cash book, an Excel workbook, or a generic foreign
ERP), the differentiator is not feature breadth. It is that the numbers are defensible and the
interface is in Bangla.

## Operating Context

- **Morning check.** The owner opens the app first thing, often on a phone, to see whether any
  shipment is going to miss its date. This is the screen that decides the purchase
  (`MASTER_PLAN.md` §5 P7), not a dashboard of charts.
- **Paper-first.** Reports are printed and signed. A4, VAT/discovery-compliant formats, printable
  output is a requirement rather than a nice-to-have (§5 P8). A dark product shell fails here.
- **Bright room, ordinary screens.** A factory office is not a dark studio; monitors are modest
  and the room has daylight (§4 gate note in `design/BRAND-DIRECTION.md`).
- **Two scripts in one sentence.** Bangla for operating language, Latin for money, account codes
  and formal reports. A clerk typing "Cotton" and a clerk typing `সূতি` must reach the same row.
- **Flaky connections.** Resilience for poor links is a stated launch requirement (§5 P8).
- **Hand-held onboarding.** A company is loaded by the vendor, not by the buyer: chart of accounts,
  opening balances at 1 July, buyers/suppliers, styles and size/colour matrices, fabric inventory,
  open orders, cash-book history (§5 P8).
- **Fiscal reality.** July–June fiscal year, twelve periods; BDT functional currency with full
  multi-currency on import of trim and fabric (§2.5). Gapless journal numbering per tenant,
  journal and period.

## Capabilities and Constraints

Confirmed, binding, and shaping every screen:

- **Module tiers.** A: ledger and accounting core, cash book, reporting, order progress and job
  costing. B: inventory/stock ledger, purchasing, sales/invoicing/payments, VAT-ready reports.
  C: payroll and HR, thin. Out of v1: APS/finite-capacity scheduling, MRP explosion, QMS,
  barcode WMS, self-serve billing.
- **Sign is data, not decoration.** Invariant I2: a line has `debit > 0` XOR `credit > 0`, never
  both, never zero, and there is no single-signed amount column. A screen must show which side a
  figure is on without the reader inferring it from a minus sign.
- **Posted means immutable.** Invariant I3: corrections are reversal entries that link back, so
  the original and its correction are both on screen. No "edit" affordance on a posted document.
- **Numbers are derived and re-derivable.** A report that disagrees with the ledger is a defect to
  display, not to hide (`Verifier` control totals, §4).
- **Money is typed per currency** with BDT at two decimal places (I7); float is not permitted.
- **Tenancy.** Every number on screen belongs to exactly one company; `Tenant` carries
  `FunctionalCurrencyCode` (default `BDT`), `PreferredLanguage` (default `bn`) and
  `FiscalYearStartMonth` (7). `CompanyProfile` carries `LegalName` and `LegalNameBangla`.
- **Four enforced UI laws the design cannot negotiate with** (`web/eslint.config.mjs`):
  L12 one env-driven API client and no browser storage outside it; L13 no `Math.abs` anywhere, no
  currency-code or BCP-47 literal outside `src/i18n/`, no `Intl.*Format` built from a literal;
  L15 colour literals exist only in `src/theme/Tokens.ts`; L17 every user-facing string goes
  through the translation layer. L14 keeps the route graph closed, so a screen that is not in the
  navigation does not exist.
- **Locale is `en` or `bn` only** (`web/src/i18n/Locale.ts`). Region-specific formatting is not a
  locale concern here; it is tenant data.
- **Backend reality today.** Only three controllers exist (`api/v1/auth`, `api/v1/departments`,
  `api/v1/ledger/journals`). Ledger read models for trial balance, GL detail, cash book, AR aging,
  job costing and order progress do not exist yet. Screens specified here are specified against the
  data the ledger kernel already guarantees, and each is marked with what it needs.
- **Explicitly undecided and not to be invented:** VAT and withholding-tax regime (blocking before
  P6); the two pilot factories' identities; which screens are Bangla-first versus English-first
  (a native Bangla review is scheduled with P9 — `MASTER_PLAN.md` §5 P2 note: a subtly wrong Bangla
  account name is worse than an English one the reader can parse).

## Brand Commitments

- `docs/MASTER_PLAN.md` §4 pins the design route: every UI task goes through the `impeccable` and
  `awesome-design-md` skills, and `design/DESIGN.md` is the single token source. It names Linear,
  Stripe and Notion as candidates to evaluate, and rejects by default decorative hover lifts on
  data cards, gradient hero cards and sparklines without a real series.
- `design/BRAND-DIRECTION.md` records the evaluated shortlist. `design/DESIGN.md` supersedes its
  recommendation and freezes the token set; where the two disagree, DESIGN.md is authoritative.
- Bangla is a first-class UI language, decided at P0, not a toggle bolted on later (law L17).
- No marketing asset may derive from mock data (L13). A screen that has no real data shows that
  fact; it does not show a plausible one.

## Evidence on Hand

- The defect list in `docs/MASTER_PLAN.md` §0 is executed-and-verified evidence about the last
  build, and it is the specification for what this design must prevent: 15 of 18 components in the
  old trial-balance folder unreachable and the routed screen the worse of the two; about forty
  inline colour literals; `Math.abs` on every figure; `FinalBalance = credits − debits`, which is
  zero for any balanced ledger; a truncated GUID under a header promising an account code;
  trial balance hardcoded `USD`/`en-US` next to a balance screen showing `BDT`; one route forcing
  `#0B1437` navy against the shared shell's `#ffffff` sidebar; a `DateRangeSelector` imported and
  never mounted, leaving the report permanently month-to-date; mock trend values and one hardcoded
  SVG sparkline path per row.
- Real client data available for a demo tenant: the pilot client's cash-book CSVs, referenced by path
  and never committed (§6). The demo tenant must be a factory, not a generic trading company.
- Absences that future work must not fabricate: no customer logos, no testimonials, no pricing
  figures, no benchmarks, no live pilot screenshots, no real chart series. `docs/progress.md` is
  known to be fiction and is not evidence.
- Verified font and formatting facts behind `design/DESIGN.md` and `design/DATA_VIZ.md` are
  recorded there with the command or file that produced them.

## Product Principles

1. **Legibility of state before decoration.** A figure's company, currency, period, sign and side
   are readable in one second, or the screen has failed regardless of how it looks.
2. **The screen must be defensible to a third party.** Every report has to survive an accountant
   or a VAT officer reading it on paper without the app open.
3. **Absence is honest.** No data means an empty state that says so and names the module that would
   fill it. Nothing that looks like a number is allowed to not be one.
4. **The owner's language, the accountant's notation.** Operating copy in Bangla; money, account
   codes and formal report structure stay in Latin. One screen serves both without either party
   translating in their head.
5. **Density is a feature.** The buyer's whole working day is long tables of numbers. Chrome is
   quiet, rows pack, and nothing that is not data takes space.

## Accessibility & Inclusion

- Bangla rendering is an accessibility requirement for this user base, not a localisation
  nicety: correct conjunct forms, adequate leading for matras and descending glyphs, and search
  that works in both scripts.
- Meaning never travels by colour alone; sign, side and label accompany every coloured state
  (WCAG 1.4.1). Text hits 4.5:1 on the surface it actually sits on; control boundaries hit 3:1.
- Keyboard-only operation for the primary flows of the accounts clerk, who lives in these screens
  all day; visible focus that is not the browser default.
- Printed output is a supported surface: a report that only works on screen is not finished.
- Zoom to 200% without horizontal loss of a figure's meaning; tables reflow rather than clip.
