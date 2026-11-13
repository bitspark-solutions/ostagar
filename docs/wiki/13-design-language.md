# 13 — Design language (`design/` + `web/src/theme`)

Design is routed through tooling per standing instruction: `impeccable` for
critique gates, `awesome-design-md` for token sets from named brands. `design/`
holds the decision records; `web/src/theme/Tokens.ts` is the only place values
exist in code (law L15).

## Files in `design/`

| File | Role |
|---|---|
| `DESIGN.md` | **The frozen token source** — status: "the token source"; supersedes `BRAND-DIRECTION.md` (kept as shortlist record) |
| `BRAND-DIRECTION.md` | The evaluated shortlist (Linear/Stripe/Notion/SAP/Carbon…) and why each was rejected |
| `PRODUCT.md` | Product framing for design decisions |
| `SCREENS.md` | Screen inventory/specs |
| `DATA_VIZ.md` | Chart/table rules for financial data |
| `AUDIT.md` | Design audit record |
| `mocks/order-progress.html`, `mocks/trial-balance.html` | HTML mocks for the two Tier-A buyer screens |

## The chosen direction (DESIGN.md §1)

**"SAP's structural restraint, Stripe's numeric discipline, and the ledger itself
deciding everything else."** Evaluated against *this buyer* (Bangladeshi factory
owner, paper-first culture, bright office, A4 printing) rather than taste:

- **SAP structure**: white canvas, two greys + one blue, flat bands not shadows,
  corners ≤8px, quiet chrome — a page that reads as an application frame.
- **Stripe numerics**: explicit tabular-figures role (`font-feature-settings:"tnum"`)
  — the only candidate spec that defines it; money columns must align.
- Rejected: Linear (near-black loses on print), Notion/Airtable (card soup — the
  legacy balance screen's ten nested Cards), IBM Carbon (no usable data-table spec),
  Stripe wholesale (dark shell, gradients = marketing vocabulary), SAP wholesale
  (licensed variables, loose type scale).

**Signature move**: every financial figure is decimal-aligned in a fixed-width
column, sign in its own slot, currency named once per screen — the habit that makes
`Math.abs`-style defects impossible to reintroduce.

Colour strategy: **Restrained** — neutrals + one accent; the chromatic budget is
spent on *accounting state* (Dr/Cr semantics), not decoration. No gradients, no dark
surfaces, no second brand hue.

## Token system (`web/src/theme/Tokens.ts`)

Single source (L15): `color` (text/surface/border/action ramps + state soft
variants), `radius` (0/4/6), `space` (**explicit px strings** — bare numbers in MUI
`sx` multiply theme spacing; the comment records the 128px-gap incident), `type`
(font CSS vars + money/label/body/title sizes), `density` (row padding, sticky
header height). DESIGN.md's final palette differs from the current neutral
placeholder — §9 of DESIGN.md lists the exact token edits to apply.

`AppFonts.ts` loads Latin + Bengali families as CSS variables so mixed-script labels
align; `AppTheme.ts`/`ThemeProvider.tsx` apply MUI theme from tokens.

## Accounting-UI rules the design must satisfy (MASTER_PLAN §4)

Each responds to a verified legacy defect:

- Signed amounts, never `Math.abs`; Dr/Cr column semantics explicit; negative in red
  **with sign present**.
- Real account codes in the Code column (legacy printed truncated GUIDs).
- `accountType`/`subType` survive DTO boundaries for normal-balance display.
- "Opening / Period movements / Closing" column set; date range actually changeable.
- Every table: sticky header, scroll container, breakpoint variants, skeleton,
  empty state, **real error state** — `catch{console.error}` → blank page is banned.
- No trend/chart/KPI without a real series; one currency per screen.
- One theme (legacy `/trial-balance` forced navy against the shared shell).

## Bangla typography (§4.1 of MASTER_PLAN, decided at P0)

- Latin pairing **and** Bengali script family with matching metrics — mixed
  `স্টক / Stock` labels must not break baseline.
- Tabular figures on all money columns.
- Amount formatting: BDT thousand-grouping per tenant setting (Western vs
  South-Asian lakh style) — stored as tenant settings, never inferred from browser
  locale (legacy hardcoded `en-US`/`USD`).
- Postgres `bn-x-icu` collation per searchable column — verified sorting
  ক, কা, খ, গ; search must work in both scripts.
- L17 translation layer exists from P0 (see [10](10-web-frontend.md)).

## Process gates

`impeccable` critique runs at the end of **each UI-bearing phase** — findings fixed
before the phase is called done; before/after captured for P9 marketing assets.
Design tokens have one source; inline hex/rgba in components fails CI (L15).
