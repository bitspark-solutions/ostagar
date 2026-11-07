# DESIGN.md — the visual language, frozen

Status: **the token source.** `docs/MASTER_PLAN.md` §4 requires `design/DESIGN.md` chosen and
applied at the P4 gate; this file is that choice. It supersedes the recommendation in
`design/BRAND-DIRECTION.md` (that file stays as the shortlist record). Where this file and
`web/src/theme/Tokens.ts` disagree, this file is the intended state and §9 lists the exact edits
the app agent must make — this is a specification, and no file outside `design/` was changed to
produce it.

Mode: **Operate** (impeccable). The visitor completes a task; expression may never obscure the
task, the state, or a familiar affordance.

---

## 1. Direction, and why this one

**SAP's structural restraint, Stripe's numeric discipline, and the ledger itself deciding
everything else.**

Derived from `awesome-design-md` specs `references/brands/sap.md` and `references/brands/stripe.md`,
evaluated against this buyer rather than against taste:

| What the surface has to survive | Which spec answers it |
|---|---|
| Reports printed on A4 in a paper-first culture, in a bright office. | SAP: white canvas, two greys and one blue, nothing dark, no atmospheric hero. Linear (`#010102`) and Stripe's dashboard track ("flips polarity to a dark-app shell") both fail this. |
| A page that reads as an application frame, not marketing. | SAP: chrome is deliberately quiet; depth comes from **broad flat bands**, not shadow; corners capped at 8px on controls; "don't elevate a button or a card". |
| Text classes distinguished at the same size in a dense table. | SAP's five-tier ink ladder that **shifts hue as it descends** (black → warm grey → slate → chrome grey), plus a 350–700 weight range. Density is carried by weight and hue, not by size, so the money column can stay small and still read. |
| Tabular figures wherever money matters. | Stripe, and only this: an explicit `body-tabular` role with `font-feature-settings: "tnum"` and tightening. Stripe is the only candidate in the library that specifies it. Its gradient mesh, pill buttons, weight-300 display tier and dark pricing card are all rejected — they are marketing vocabulary on a working screen. |
| Two scripts in one cell. | Neither spec has it. Solved in §3 and §4 from the font metrics actually measured. |

Rejected outright, with the reason, so it is not re-litigated: **Linear** (near-black canvas loses
on print and in daylight); **Notion / Airtable** (illustration-rich card surfaces — card soup is
exactly what produced the legacy balance screen's ten nested `Card` components); **IBM Carbon**
(its own `Known Gaps` note says the real Carbon data tables are not in the analysis, so there is
nothing usable to take); **Stripe wholesale** (dark dashboard shell, decorative gradient);
**SAP wholesale** (72 Brand Variable is licensed, and its marketing typography scale at 18–20px
body is far too loose for a ledger).

Nothing is copied wholesale. The palette below is a derived system: SAP's *structure* (banded
greys, one acting blue, flat elevation, hue-shifting ink) with the values adjusted so every rung
passes WCAG on the surface it actually sits on, and Stripe's *money rule* promoted to a token.

**Colour strategy** (impeccable new-work §4): **Restrained** — neutrals plus one accent, because
this is a surface people operate. But the accent is not decorative, and the semantic set is not
one colour: on a ledger, colour carries accounting meaning, so the palette spends its chromatic
budget on state and keeps everything else neutral. There is no second brand hue and no gradient
anywhere.

**Signature move (the one thing the world contributes, Operate rules):** every financial figure is
**decimal-aligned in a fixed-width column with the sign in its own slot and the currency named once
per screen, not per cell.** That is not a colour scheme; it is a habit visible on every table in the
product and nowhere else in the market's Excel workbooks. It is what makes a column of taka readable
at a glance and what makes the legacy `Math.abs` defect impossible to reintroduce.

---

## 2. Colour tokens

Every value below exists in `web/src/theme/Tokens.ts` and nowhere else (law L15). Contrast figures
are computed from these exact hex pairs with the standard WCAG relative-luminance formula
(srgb linearisation, `(L1+0.05)/(L2+0.05)`); they were computed for this document, not assumed, and
each is stated against the surface it is specified to sit on.

### 2.1 Surfaces

| Token | Hex | Role | Measured |
|---|---|---|---|
| `surfaceBase` | `#ffffff` | The working canvas. Content panes and report bodies. Never tinted. | — |
| `surfaceMuted` | `#f5f6f7` | **Chrome.** Table headers, filter bars, side rail, toolbar, the identity strip. Not a card background. | 1.08:1 on white — deliberately near-invisible; it groups by tone, not by contrast |
| `surfaceRaised` | `#fbfcfd` | A pane lifted one step off the canvas: popovers, menus, a report body inside a muted page frame. | — |
| `surfaceBand` | `#eaecee` | **Totals and group breaks.** Subtotal rows, the report footer strip, a period-closed banner. Reserved: it may not be used for decoration. | 1.18:1 on white; `textPrimary` on it 15.44:1 |
| `surfaceSelected` | `#ebf8ff` | The active row, the active nav item, a selected filter chip. One selection per screen at a time. | `textPrimary` 16.43:1, `accentBrand` 5.89:1, `textTertiary` 4.98:1 |

`surfaceMuted` changes from `#f2f4f7` to `#f5f6f7` (§9). The reason is not taste: the system needs
**two** greys doing **two** different jobs — chrome versus totals — and one grey cannot do both
without a reader losing the ability to tell a header from a subtotal, which is a financial
distinction, not a visual one.

No dark surface exists in this system. There is no `surfaceInverse` and no polarity-flipped
component.

### 2.2 Ink

| Token | Hex | Role | On `surfaceBase` | On `surfaceMuted` | On `surfaceBand` |
|---|---|---|---|---|---|
| `textPrimary` | `#111825` | Headings, **all money**, account names, any figure the reader must act on. | 17.78 | 16.43 | 15.44 |
| `textSecondary` | `#5b6472` | Labels, column headers, table metadata, captions. | 5.98 | 5.53 | 5.05 |
| `textTertiary` | `#616b7a` | **De-emphasis only**: units, timestamps, "as of", helper text, a disabled-adjacent hint. Never on a figure or a required label. | 5.40 | 4.99 | 4.56 |
| `textInverse` | `#f7f9fb` | Text on a filled `actionNeutral` or `accentBrand` control. | — | — | — |
| `chromeInk` | `#545454` | Navigation labels and breadcrumbs only. A neutral grey rather than a slate, so navigation reads as chrome and content reads as content at the same size. Never for body copy. | 7.57 | 7.00 | — |

`textPrimary`, `textSecondary` and `textInverse` keep their existing values. `textTertiary`,
`chromeInk` and the band/selected surfaces are new (§9). `chromeInk` is SAP's `ink-nav`, the one grey
in the ladder with no blue in it, and it is the cheapest way to buy a fifth text rung in a dense
table without buying a fifth font size.

### 2.3 Boundaries

| Token | Hex | Role | Measured vs white |
|---|---|---|---|
| `borderSubtle` | `#dfe3e8` | **Row rules** in every table, 1px, bottom-only. Decorative: it must never be the only thing separating two groups of information. | 1.29 |
| `borderStrong` | `#7d8898` | Control boundaries (inputs, selects, outlined buttons), the vertical rule between a Dr and a Cr money column, and the outer rule of a report block. | 3.59 (3.32 on muted, 3.03 on band) |
| `borderFocus` | `#0040b0` | The 2px focus ring (§7). | 8.90 |

`borderStrong` changes from `#b6bec9` (§9). At 1.88:1 the old value fails WCAG 1.4.11's 3:1
requirement for the visual boundary of a user-interface component; the accountant's input box was
effectively invisible on paper and in bright light. The new value clears 3:1 on all three light
surfaces.

No `border-left` / `border-right` colour tint above 1px on cards, list items, callouts or alerts —
it is a banned device and it is also how the legacy screens ended up with forty colour literals.

### 2.4 The one acting colour

| Token | Hex | Role | Measured |
|---|---|---|---|
| `accentBrand` | `#0057d2` | Links, the active tab underline, the selected-row indicator, the progress bar fill, focus-visible on primary controls, the "current period" marker. | 6.38 on white, 5.89 on muted, 5.38 on band; white on it 6.38 |
| `accentBrandDeep` | `#0040b0` | Link visited/pressed, the outline button's label on white, the focus ring, the rule under an active tab. | 8.90 on white |
| `actionNeutral` | `#33415c` | **Primary button fill.** A slate, not the accent: a filled button is a commitment, and the accent must stay free to mean "this is where you are". | `textInverse` on it 9.70 |
| `actionNeutralHover` | `#26324b` | Hover on a primary fill. | — |
| `actionNeutralPress` | `#1b2537` | Press on a primary fill. | — |

Two blues and one slate is the entire chromatic budget of the shell. There is no green, no purple,
no brand gradient, no icon tint. A second accent hue is forbidden; the SAP spec's own "Don't" list
says the same, and here it is also a functional rule: if blue can mean "selected" and "chart" and
"link" and "brand", the reader cannot tell which of the four he is looking at.

### 2.5 State colours, and what colour is never allowed to mean

| Token | Hex | Meaning | Measured |
|---|---|---|---|
| `stateDanger` | `#a31621` | **A signed negative on an asset/natural-debit account, an overdue shipment, a period lock violation, a validation failure, a `Verifier` drift alarm.** | 7.80 white, 7.20 muted, 6.58 band |
| `stateDangerSoft` | `#fdecee` | Background of a danger chip or a rejected row. | `stateDanger` on it 6.83 |
| `stateWarning` | `#8a5300` | At risk but not yet failed: within 10 working days of ship date and behind plan; an abnormal balance; an unassigned import row. | 6.33 white, 5.76 on soft |
| `stateWarningSoft` | `#fdf3e2` | Its background. | `stateWarning` on it 5.76 |
| `stateSuccess` | `#14663d` | **Only** "a fact was recorded": save confirmed, journal posted, import completed, dispatch done. Never on a money column. | 7.01 white, 6.26 on soft |
| `stateSuccessSoft` | `#e9f5ee` | Its background. | `stateSuccess` on it 6.26 |
| `stateInfo` | `#0b5394` | Neutral disclosure: a prior-period adjustment, a foreign-currency line, an advisory from the ledger. | 7.84 white |
| `stateInfoSoft` | `#eaf2fb` | Its background. | `stateInfo` on it 6.94 |

**Colour never encodes debit or credit.** This is the central rule of the system and it is a
deliberate change from `design/BRAND-DIRECTION.md`, which reserved a green/red pair for Dr/Cr
semantics. Two reasons, both decisive:

1. A credit is not bad news. A credit to a liability is normal, and a credit to an asset is an
   alarm — the same colour would mean both, and a reader who has learned "red = credit" will
   mis-read a bank overdraft and a loss alike.
2. Reserve red for adversity, and a signed negative in red **with the sign printed** becomes
   unambiguous information rather than decoration.

Debit and credit are carried by **column position, a sticky header naming the side, and a 1px
`borderStrong` vertical rule between the two money columns** (§6 of `DATA_VIZ.md`). Colour then
means one thing only: this figure is adverse, or this state is bad.

**Every coloured state also carries text.** A chip reads `At risk`, not a red dot. A negative figure
prints its minus. This is WCAG 1.4.1 and it is also the reason the screen survives a monochrome
laser printer in a factory office.

### 2.6 Data series

Only two chart families are permitted in v1 (see `DATA_VIZ.md` §5): the aged AR/AP bucket strip and
the order-progress stage bar. Four tokens, one hue ramp plus the semantic set:

| Token | Hex | Role |
|---|---|---|
| `seriesPrimary` | `#1d5fbf` | The largest/first series, the on-track portion of a progress bar. 6.10 on white |
| `seriesMid` | `#3d7bd0` | Middle bucket. 4.25 on white |
| `seriesSoft` | `#c7dcf3` | Smallest buckets, the unfulfilled remainder of a bar. Fill only — no text on it |
| `seriesNeutral` | `#b6bec9` | "No data", "not applicable", a bar's zero. |

Warm and red series come from `stateWarning` / `stateDanger`; they are never used for a series that
is not a risk. A legend label is always `textPrimary` next to the swatch, never white-on-swatch.

---

## 3. Typography

### 3.1 Families, verified rather than assumed

| Role | Family | Where it came from |
|---|---|---|
| Latin and all digits | **Inter** | Already loaded via `next/font/google` in `web/src/theme/AppFonts.ts`. |
| Bengali script | **Noto Sans Bengali** | Already loaded in the same file. |
| Final fallback | `system-ui, sans-serif` | `Tokens.type.fallbackFont`. |

Verification, with the check that produced each fact:

- **`Noto Sans Bengali` is a real Google Fonts family and `Noto Sans Bangla` is not.** Requesting
  `https://fonts.googleapis.com/css2?family=Noto+Sans+Bengali:wght@100..900` returns HTTP 200 with
  `@font-face` rules sourced from `fonts.gstatic.com/s/notosansbengali/v33/…`. The same request for
  `family=Noto+Sans+Bangla` returns **HTTP 400**. That naming trap is exactly the failure the brief
  warns about: Google names Noto families by *script adjective* ("Bengali") while naming Anek's by
  *script name* ("Anek Bangla"). The word "Bangla" is correct in prose and wrong in a font request.
- **The family is genuinely available to this app, not just to Google.** `web/node_modules/next/dist/compiled/@next/font/dist/google/font-data.json`
  contains entries for `Noto Sans Bengali`, `Noto Serif Bengali`, `Anek Bangla`, `Tiro Bangla`,
  `Hind Siliguri` and `Baloo Da 2`; and a real build on this machine produced
  `.next/dev/static/chunks/[next]_internal_font_google_noto_sans_bengali_…module_css` plus woff2
  assets in `.next/dev/static/media/`. So the font is not a hopeful name: it has been downloaded.
- **Its coverage is the whole Bengali block.** The generated `@font-face` declares
  `unicode-range: U+951-952, U+964-965, U+980-9FE, …`, i.e. Bengali U+0980–U+09FF, which contains
  Bangla digits U+09E6–09EF and the taka sign U+09F3. `font-weight: 100 900`, so it is a variable
  axis and the 350/450/500 weights below are available in Bangla too.
- **Inter has no Bengali glyphs.** Its generated CSS serves only `latin`, `latin-ext`, `greek`,
  `greek-ext`, `cyrillic`, `cyrillic-ext` and `vietnamese` ranges — no `U+980` range at all. This is
  why the stack order in `web/src/theme/AppTheme.ts` (Latin first, Bengali second) is the correct
  mechanism and must not be reversed.
- **Inter has `tnum`; Noto Sans Bengali does not.** Parsing the GSUB `FeatureList` of the static TTF
  instances served by `fonts.gstatic.com`: Inter exposes `ccmp, calt, dnom, frac, locl, numr, pnum,
  tnum`; Noto Sans Bengali exposes `abvs, akhn, blwf, blws, ccmp, cjct, half, init, locl, nukt, pres,
  pstf, rclt, rlig, rphf, vatu` — **no `tnum`**. Consequence, and it is a rule not a preference:
  Bangla numerals cannot be made to align in a money column, so **every financial figure in this
  product is set in Latin digits regardless of UI language** (§3.4).

No proprietary substitution risk is being taken: 72 Brand Variable and Sohne are both licensed and
neither is used. Inter is the closest open analogue to 72's width and x-height, which is the
substitute SAP's own spec recommends.

**The one metric problem with the substitute, and its fix.** SAP's system leans on a genuine
sub-400 weight: `list-item` at 350 versus `nav-link` at 400, same size, different class of text —
and SAP's "Don't" list warns that flattening the range to 400/700 collapses three distinct roles in
the 14–16px band. Inter Variable reaches 100–900, so 350 and 450 are available and are used below.
This is only true if the *variable* instance ships; the static instance must carry the weights 350
and 450 explicitly, or the sub-400 role silently collapses to 400 and the ladder breaks. The app
agent must request both axes from `next/font/google` and confirm the served file's weight range
(§9, item T-13).

### 3.2 Type scale

Base size 14px. There is no display tier: this product has no hero, and a 57px headline on a trial
balance is a lie about what the screen is for.

| Token | Size / line height | Weight | Role |
|---|---|---|---|
| `type.pageTitle` | 20px / 28px | 600 | The one `h1` per route. |
| `type.sectionTitle` | 15px / 22px | 600 | A block heading inside a screen — "Opening / Period movements / Closing". |
| `type.body` | 14px / 20px | 400 | Default UI body, prose in a panel. |
| `type.bodyLead` | 14px / 22px | 450 | A sentence the reader must not skim: a variance explanation, a drift alarm. |
| `type.tableCell` | 14px / 20px | 400 | Text cells: account name, particulars, buyer, style. |
| `type.tableCellStrong` | 14px / 20px | 500 | A row the eye must stop on: a total, a selected row, an over-budget style. |
| `type.money` | 14px / 20px, `tnum` | 400 | **Every amount.** Fixed-width digits, sign in its own slot. |
| `type.moneyTotal` | 14px / 20px, `tnum` | 600 | A subtotal, a carried balance, a report total. |
| `type.tableHeader` | 12px / 16px | 600 | Sticky column header. `textSecondary` on `surfaceMuted`. |
| `type.label` | 12px / 16px | 500 | Form label, filter label, chip text. |
| `type.meta` | 12px / 16px | 400 | Timestamp, "as of", unit, row helper. `textTertiary`. |
| `type.navItem` | 14px / 20px | 400 | Sidebar rail item, breadcrumb, tab. `chromeInk`. |
| `type.navItemActive` | 14px / 20px | 500 | The active rail item — heavier, not coloured-and-heavier. |
| `type.button` | 14px / 16px | 500 | Button label, `textTransform: none` (already the theme's setting). |
| `type.code` | 13px / 20px, `tnum` | 500 | Account codes, journal numbers, BIN/e-TIN, a document reference. Digits and letters, aligned, selectable. |

Two sizes below 12px are not permitted anywhere, and the ladder stops at 12. A 10px "micro-cap"
eyebrow is a marketing device and the legacy screens' tiny labels are why an accountant had to
lean in. Nothing in this system is uppercase-tracked as a label style: Bangla has no case, so an
all-caps eyebrow is untranslated by construction.

Line height is set per token, never left to the browser default, and the Bangla rule in §3.3
overrides the numeric value for `bn`.

### 3.3 Bangla: how the system stays readable

Bangla is not Latin at a different vocabulary, and the difference is vertical.

**Measured font metrics** (parsed from `hhea` and `OS/2` of the TTF instances served by
`fonts.gstatic.com`, normalised to units-per-em):

| Font | upem | ascender | descender | natural line box |
|---|---|---|---|---|
| Inter | 2048 | 1984 | −494 | **1.210 em** |
| Noto Sans Bengali | 1000 | 917 | −408 | **1.325 em** |

So a Bengali line box is **9.5% taller than a Latin one at the same font size**, before any
composed consonant form is counted — and a Bangla syllable such as `গ্য`, `ক্ল` or a matra-plus-
subjoined combination needs the full box, because its marks sit above the ascender line and its
descender forms below the baseline. Inter's metrics are the ones a browser uses for `line-height:
normal` when Inter is first in the stack, which means a naive dense table at 1.25 leading **clips
the top of Bengali matras and collides the bottom of descending forms between rows** — the exact
"mixed label breaks the line height" defect.

Rules, in order:

1. **Minimum leading for a cell containing Bangla is 1.45; for prose 1.65.** 1.45 × 14px = 20.3px,
   which clears the measured 18.55px box with room for the jukta/eyecatch region.
2. **Row heights are fixed to absorb both scripts, so switching language does not move the grid.**
   A compact row is 32px, comfortable 40px: 20.3px of Bangla leading + `density.tableRowPaddingY`
   8px top and bottom + the 1px rule = 37.3px, which is why the **compact** density is 8px/4px
   padding and not the legacy 8px. See §4.2; the numbers were checked against each other rather
   than asserted.
3. **Baseline alignment in a mixed label is a leading concern, not an alignment concern.** Latin
   and Bengali glyphs in the same line resolve from two files with different ascenders, so
   `align-items: baseline` inside a flex row of mixed-script labels is not sufficient — the row
   must have the Bangla leading applied whenever it can contain Bangla, which is decided per
   locale, not per string: **`layoutLocale = 'bn'` applies the Bangla leading to the whole
   document.** Per-string detection would make two adjacent rows in the same table carry different
   grid heights, which reads as a broken table.
4. **No negative letter-spacing anywhere in Bangla.** Bengali conjunct formation depends on normal
   advance widths; tracking tightens the shaping and `half`/`rphf` forms misrender. Because this
   system uses zero tracking everywhere (§1, SAP's rule), the constraint costs nothing — which is
   the reason for adopting it globally rather than gating it per script.
5. **No italic for emphasis.** Bengali italics are synthetic slants in Noto and break conjunct
   reading. Emphasis is weight (`bodyLead` 450, `tableCellStrong` 500) or colour, never slope.
6. **Font stack order stays `latin, bengali, system-ui`.** Reversing it means Latin digits render
   from Noto Sans Bengali (it does cover basic Latin), and those digits are a different width from
   Inter's, so a money column stops aligning while still looking fine in a screenshot.
7. **Numbers, codes and currency stay Latin** (§3.4).
8. **Sorting and search are verified with real Bangla data, not assumed.** `bn-x-icu` collation is
   available on the deployed Postgres and was proven to sort ক, কা, খ, গ in varnamala order
   (`MASTER_PLAN.md` §5 P0 record, item 5). Search must hit the same row from "Cotton" and from
   `সূতি`; that is a backend concern, and the UI consequence is that a search field's placeholder
   and its empty result must both name the language it searched in.
9. **Native Bangla bookkeeping terms are a P9 dependency, not this document's invention.** Account
   names, journal names and the Dr/Cr column headings carry conventional wording a non-native
   speaker gets subtly wrong, and `MASTER_PLAN.md` §5 P2 already schedules a native review. This
   spec fixes the *shape* of those strings (length, leading, abbreviation width) so the review can
   land without a layout change. §3.5 states the length budget.

### 3.4 Digits and money in type

- **Latin digits always, in both locales.** Enforced by `numberingSystem: 'latn'` in the formatter,
  not by convention — see `DATA_VIZ.md` §3, where the measured output of the naive call is shown to
  be `১২,৩৪,৫৬৭.৫০৳`.
- **Bangla numerals appear nowhere in this product** except in free text a user typed himself
  (a particulars line, a note). They are never generated by the app for a figure, a date in a
  report, a page number, or a count.
- `tnum` is applied by the `type.money` and `type.code` roles, not by a helper class a component may
  forget. `.tnum` in `web/src/app/globals.css` becomes an implementation detail of those two roles.
- Currency symbols are set in `type.meta`/`type.money` from Inter — ৳ is U+09F3, which Inter does
  not contain, so the symbol falls back to Noto Sans Bengali **even in the English UI**. This is
  correct and must not be "fixed" by forcing the Latin font: the alternative is a tofu box.
  The identity strip names the currency in Latin (`BDT`) for exactly this reason.

### 3.5 String length budget

Because Bangla and English differ in length for the same label, and a header that wraps turns a
ledger column into a two-storey row:

| Element | English budget | Bangla budget | Consequence |
|---|---|---|---|
| Table column header | ≤ 18 chars, one line | ≤ 16 chars, one line | Longer means the column widens or the header abbreviates; abbreviate at the *message key* level, never in JSX. |
| Nav rail item | ≤ 20 chars | ≤ 20 chars | Rail width is fixed at 240px; overflow ellipsises, and the tooltip carries the full string in both scripts. |
| Button label | ≤ 20 chars | ≤ 24 chars | Buttons size to content; a long Bangla label must not push a sibling off the toolbar. |
| Status chip | ≤ 12 chars | ≤ 14 chars | Chips sit inline in a row; a wrapping chip changes row height and breaks the grid. |
| Page title | ≤ 40 chars | ≤ 48 chars | One line at 1280px. |

If a string cannot say the thing inside the budget, the budget is the finding: shorten the copy or
restructure the screen. Do not silently allow a wrap in one locale and file the difference under
"translation problem".

---

## 4. Spacing, density, layout

### 4.1 Spacing scale

Carries **explicit units** like the existing `space` tokens do, for the reason recorded in the
`Tokens.ts` comment: MUI's `sx` multiplies a bare number by `theme.spacing`, so a token meaning
"16 of something" is not a single source of truth.

| Token | Value | Role |
|---|---|---|
| `space.xxs` | `2px` | Gap between a sign slot and its digits; icon-to-label inside a chip. |
| `space.xs` | `4px` | Compact row padding; gap between a label and its unit. |
| `space.sm` | `8px` | Intra-group gap: filter chips, a button pair, a form's helper line. |
| `space.md` | `12px` | Between related fields; cell horizontal padding at comfortable density. |
| `space.lg` | `16px` | Default block padding. Card interior. |
| `space.xl` | `24px` | Between blocks in a screen. More space above a heading than below it. |
| `space.xxl` | `32px` | Between screen sections; empty-state interior. |
| `space.huge` | `48px` | Between a page title and the first block. Onboarding step separation. |

Rule the system enforces: **a heading gets `space.xl` above and `space.sm` below.** The legacy
screens' flat rhythm — the same gap everywhere — is why nothing stood out.

### 4.2 Density

Three densities over one table component. Density is a **user×route preference stored per
tenant**, not a per-screen invention.

| Token | Compact | Default | Comfortable |
|---|---|---|---|
| `density.tableRowMinHeight` | 32px | 40px | 48px |
| `density.tableRowPaddingY` | 4px | 8px | 12px |
| `density.cellPaddingX` | 8px | 12px | 16px |
| `density.stickyHeaderHeight` | 36px | 44px | 44px |
| `density.stickyHeaderHeightTwoTier` | 72px | 88px | 88px |
| Rows visible at 900px viewport | ~26 | ~20 | ~16 |

A trial balance and a GL detail use a **two-tier header** (a group band over the Dr/Cr pair), so they
carry the second token: each tier keeps `stickyHeaderHeight` and the whole band is double. Both tiers
are `position: sticky`, and **the second tier must be offset by the first tier's height, not by
`top: 0`** — sticking both to zero lays the group band over the side labels the moment the table
scrolls. The mock caught this; the token table had promised one height for a header that needs two.

The row height is a **floor, not a clamp**: a cell that needs a second line grows the row by exactly
one leading step (20px). Two-line cells are **forbidden at compact density** — a financial report at
32px rows has one line per cell and nothing else, and a note that needs more words belongs in a
footnote below the table or in the print footer, not inside the grid. That restriction is the reason
compact can promise a row count.

`density.tableRowPaddingY` keeps its existing `8px` as the *default*; compact is a new value and the
existing 8px was what made an 80-line trial balance need eight screens. **Financial reports default
to compact; the order-progress morning screen defaults to default density** (it has fewer rows and
each row carries a status chip and a second line under the date, which compact would refuse).

### 4.3 Layout

| Token | Value | Role |
|---|---|---|
| `layout.topBarHeight` | `48px` | Application top bar; contains the identity strip (§5). |
| `layout.railWidth` | `240px` | Left navigation rail. |
| `layout.railCollapsedWidth` | `56px` | Below 1100px; icon-plus-first-word, tooltip with the full string. |
| `layout.contentMaxWidth` | `1200px` | Kept, for forms, settings and onboarding. |
| `layout.reportMaxWidth` | `none` | **New. Financial reports are full-bleed inside the rail.** A 1200px cap puts a six-column trial balance into horizontal scroll on a 1366px office monitor, which is how a clerk loses the Dr/Cr rule. |
| `layout.formWidth` | `384px` | Kept for single-column forms (login, a mapping row). |
| `layout.wizardWidth` | `720px` | New: the opening-balance import, which is a table plus a mapping, not a form. |
| `layout.moneyColumnMinWidth` | `112px` | New: the narrowest a signed, 2-dp, lakh-grouped BDT column may be. |
| `layout.identityStripMinWidth` | `520px` | New: enough room for company, period, currency and as-of on one line. |

Structure (Operate mode: the world never supplies the navigation model or the controls):

```
+--------------------------------------------------------------+
| top bar 48px   [company name]  [FY period]  [BDT]  [as-of]   |  surfaceMuted + chromeInk
+-----------+--------------------------------------------------+
|           |  page title (type.pageTitle)                     |
| rail      |  ------------------------------------------------|
| 240px     |  filter bar: period | journal type | account     |
| surface   |  ------------------------------------------------|
| Muted     |  REPORT BODY  full-bleed, compact density        |
| nav items |  sticky header, 1px borderSubtle row rules,      |
| type.nav  |  1px borderStrong rule between Dr and Cr         |
| Item      |  ------------------------------------------------|
|           |  totals row: surfaceBand, type.moneyTotal        |
+-----------+--------------------------------------------------+
```

Breakpoints (MUI defaults, named for this product's real devices):

| Range | Behaviour |
|---|---|
| ≥ 1440px | Rail + full-bleed report. Nothing stretches; gutters absorb width. |
| 1100–1439px | Default. Rail 240px. |
| 900–1099px | Rail collapses to 56px. Reports gain horizontal scroll inside `TableContainer`. |
| 600–899px | Rail becomes a drawer. Reports switch to the **stacked record layout** (§4.4). |
| < 600px | Drawer. The morning order-progress screen is the only one designed mobile-first; it is the one the owner actually opens on a phone. |
| print | §8. |

### 4.4 The stacked record layout

Below 900px a financial table does not become a card grid; it becomes one record per block, with
the **field label in the left column and the signed figure right-aligned in the right column**, so
decimal alignment survives the reflow. A row that would have been `Account | Code | Dr | Cr`
becomes:

```
1111 — Bank and cash in hand          [At risk]
  Debit      12,34,567.50
  Credit              0.00
  Balance    12,34,567.50 Dr
```

This is not a mobile concession; it is also the layout used in the printed A4 "one page per account"
variant, and in the WhatsApp order-status message from P8.

---

## 5. The identity strip

Non-negotiable, on every screen that shows a figure, and the single most important structural
answer to the legacy defects. One line, `type.meta` at 12px, `textSecondary`, on `surfaceMuted`,
`layout.topBarHeight` 48px, four items in a fixed order, separated by a 1px `borderSubtle` rule and
`space.md` of air:

```
[ Demo Garments Ltd ]  [ FY 2026-27 · Period 4 (Oct) ]  [ BDT ]  [ Position as at 31 Oct 2026, 09:12 ]
```

- **Company** — `CompanyProfile.LegalName`, or `LegalNameBangla` when the locale is `bn`. Not the
  tenant slug.
- **Fiscal period** — from `Tenant.FiscalYearStartMonth` = 7, so July is Period 1. Printed as both
  the year and the period number because the VAT officer asks for the period and the owner asks for
  the month.
- **Currency** — `Tenant.FunctionalCurrencyCode`, as the ISO code in Latin. Never a symbol here; the
  code is the thing two adjacent reports must agree on.
- **As-of** — every projection is keyed by `as_of`; the strip shows it and its time. A report with
  no as-of date is a report whose freshness nobody can state.

Rules: the strip is **not** a per-screen component with its own strings; it is one component reading
tenant context, so two screens cannot disagree about which company they are in. It repeats as the
print page header on every printed page. When more than one tenant is reachable for the user, the
company item is the switcher, and the strip is the only place that switch is allowed to live.

---

## 6. Radius, elevation, borders

### 6.1 Radius

| Token | Value | Role |
|---|---|---|
| `radius.none` | `0` / `0px` | Every full-width region: the top bar, the rail, a band, the table, a report block. A rounded band reads as a card; a square one reads as a zone, and zones are what this layout is made of. |
| `radius.small` | `4` / `4px` | Chips, tags, inline anchor surfaces, table chrome. |
| `radius.medium` | `6` / `6px` | Every control: buttons, inputs, selects, the menu surface. |
| `radius.large` | `12` / `12px` | Modal dialogs and the import drop zone. The only place 12 appears. |

Nothing is a pill. No circle except a checkbox. The existing numeric tokens stay numeric for
`theme.shape.borderRadius`, and **each gets an explicit-unit sibling** (§9): MUI multiplies a bare
number in `sx.borderRadius` by `theme.shape.borderRadius` — verified in
`web/node_modules/@mui/system/borders/borders.js`, which builds the transformer with
`createUnaryUnit(props.theme, 'shape.borderRadius', 4, 'borderRadius')`. With the theme's current
`shape.borderRadius: 6`, `sx={{ borderRadius: 4 }}` silently means **24px**. This is the identical
trap `Tokens.ts` already documents for spacing, and it is currently live in the token block: the
only reason `EmptyState.tsx` survives is that it hand-appends `px`. A token that means "6 of
something" is not a single source of truth.

### 6.2 Elevation

Flat by default. Three levels, and level 1 is the common one.

| Level | Treatment | Used by |
|---|---|---|
| 0 | No border, no shadow | Table rows, bands, page sections. Grouping is done by `surfaceMuted` / `surfaceBand` tone and by `borderSubtle` rules. |
| 1 | `border: 1px solid borderSubtle`, no shadow | Panels, menus, popovers, the filter bar, an outlined `Paper`. |
| 2 | `0 1px 2px rgba(17,24,37,0.06), 0 2px 6px rgba(17,24,37,0.06)` | Modal dialogs, the import drop zone, a toast. Nothing else. |

The shadow colour is the ink hue rather than black, so it sits on a white canvas without greying the
type. These two box-shadow strings are **tokens** (`elevation.level1`, `elevation.level2`) because
law L15 bans an `rgba()` literal anywhere outside `Tokens.ts` — an elevation scale that cannot be
expressed in the token file is not a scale. **There is no hover elevation anywhere in this system.**
A data card that lifts when you point at it is a lie about what data is, and it is explicitly
rejected by `MASTER_PLAN.md` §4.

### 6.3 Border rules

- Tables: 1px `borderSubtle` bottom-only on every row. Header gets a 1px `borderStrong` bottom rule.
  No vertical rules except between the Dr and Cr money columns, and between a text column group and
  the money group.
- Inputs: 1px `borderStrong`, `radius.medium`; focus replaces the border with a 2px `borderFocus`
  ring plus `space.xs` offset. Never a thick border, which shifts layout.
- Report block: 1px `borderSubtle` outer rule on `surfaceBase`.
- A "no border" surface must not be relied on for grouping; grouping is tone plus `space.xl`.

---

## 7. States

Every table in the product must implement all of these; the requirement comes from
`MASTER_PLAN.md` §4 and it is what the legacy trial-balance folder failed.

**Focus.** One mechanism everywhere: `box-shadow: 0 0 0 2px surfaceBase, 0 0 0 4px borderFocus`
(a 4px ring with a 2px gap, so it reads on any surface under it), plus `outline: none` only when the
ring is applied. `:focus-visible` for keyboard, and the ring is never removed from a control that
submits money. Contrast 8.90:1 on white. Table rows are not focusable; their link cell is.

**Hover.** Text: `textPrimary` stays, the row background becomes `surfaceMuted` **only in a
selectable table**, and a link gains an underline (links are underline-on-hover, never colour-only).
Nothing moves. No transform, no shadow, no icon reveal, no tooltip that appears in under 400ms and
disappears when the cursor crosses two pixels.

**Pressed.** Filled controls darken one step (`actionNeutralHover` → `actionNeutralPress`); a
`borderStrong` outline control gets a `surfaceMuted` fill. Press state must be distinguishable from
focus state — it is, because press changes fill and focus adds a ring.

**Disabled.** `textTertiary` on `surfaceMuted`, `borderSubtle` border, `cursor: not-allowed`,
`aria-disabled` plus the real `disabled`, and **the reason stays readable**: a disabled "Post"
button on a closed period carries the tooltip/message `Period 4 is locked; post into Period 5` —
a string in the catalogue, not a greyed box the clerk has to guess at. Never reduce opacity below
the contrast floor by making disabled text transparent; use the tokens.

**Loading.** Skeleton rows, not spinners, for tables: the header, the column rules and N skeleton
rows render at real row height so the grid does not jump when data lands. A skeleton's shimmer is
`surfaceMuted` to `surfaceBand`; never a grey outside the token set. Skeleton duration budget 1.2s
before the screen admits it is slow with an inline "Taking longer than expected — retry" line. A
spinner is correct only for a button-level action, and the button keeps its label, disabled, with a
leading 16px indicator.

**Empty.** Three distinct empties, and the product must never confuse them:
- **No records exist yet** for a fresh tenant: `EmptyState` with the title naming what is missing and
  the body naming the module that writes it, plus the route to it. Already the shipped
  `common.emptyBody` pattern; keep it.
- **Records exist but the filter excludes them**: a different message — `No entries in Period 4 for
  journal type Cash` — plus one click that clears the filters. Showing "no data yet" here is a lie
  about the ledger.
- **The report is legitimately zero**: `0.00` and the totals, never an empty state. A trial balance
  with no movements in a period is a real report; hiding it is how a clerk mis-reads silence for
  absence.

**Error.** A real error state, always: the message names the problem **and** the recovery, the
retry control is present, and anything the user typed survives. `catch { console.error }` rendering
a blank page under a live header is banned, and so is the worse variant — a plausible-looking empty
report with no message. An invariant refusal from the database (I1–I8) surfaces as the ledger's own
reason, in the catalogue, naming the constraint in human terms:
`Line 3 must be a debit or a credit, not both.` Never a raw `RAISE EXCEPTION` string, never a
status code as the whole message.

**Partial data — the state this product needs and most systems lack.** When a projection is stale,
a `Verifier` control total disagrees, or an import ran but did not finish, the screen shows the
figures it has **plus a `stateWarning` or `stateDanger` band that states the disagreement and its
number**: `Report total differs from the ledger by BDT 0.02. Do not file this.` (Full rules:
`DATA_VIZ.md` §7.) A report that disagrees with the ledger and looks calm is the single most
dangerous screen in the category, and the legacy build shipped exactly that.

**Stale/blocked.** A period-locked screen renders read-only content on a `surfaceBand` banner with
the lock tier named (soft / hard / tax). A prior-period adjustment is permitted but is disclosed at
the top of the report, not buried in a footnote.

---

## 8. Print

Because reports go on paper and a factory office owns a printer, print is a first-class surface, not
a fallback.

- `@media print`: top bar, rail, filter bar, buttons, sort carets, row-select checkboxes and the
  mobile drawer all hidden. Content goes full-bleed with `12mm` margins on A4 portrait; **trial
  balance, GL detail and any ≥5-column report print landscape**.
- The identity strip becomes the page header and repeats on every page, along with the sticky table
  header (a `thead` repeats naturally; the spec requires the header row to be a real `thead`).
- Backgrounds are not printed by default: `surfaceMuted` and `surfaceBand` become the 1px `borderStrong`
  rule treatment instead. `print-color-adjust: exact` is permitted **only** for a status chip, which
  also carries its text label, so a monochrome printout loses nothing.
- Money keeps its grouping and its sign on paper. A lakh-grouped figure printed from a Western-
  grouped tenant is a document defect; the tenant setting is what prints.
- Page-break rules: never inside a journal's line group; a subtotal row stays with at least two of
  its rows (`break-after: avoid` on the group's last row, `break-inside: avoid` on the group).
- The footer strip carries: as-of timestamp, user who printed, page `n` of `m`, and the control-total
  line `Σ debits = Σ credits = 12,34,567.50` — so a page separated from its bundle can be identified.

---

## 9. Token file changes required

This is the exact handoff for the app agent. Nothing in `web/` was modified to write this spec.
`web/src/theme/Tokens.ts` stays the only place colours exist; the file's shape grows.

### Changed values

| ID | Token | From | To | Why |
|---|---|---|---|---|
| C-1 | `color.surfaceMuted` | `#f2f4f7` | `#f5f6f7` | Frees `surfaceBand` to mean "totals" and `surfaceMuted` to mean "chrome". |
| C-2 | `color.borderStrong` | `#b6bec9` | `#7d8898` | 1.88:1 fails WCAG 1.4.11 for a control boundary; 3.59:1 passes on all three light surfaces. |
| C-3 | `color.focusRing` | `#33415c` | `#0040b0` | The focus ring must not be the primary button's own fill colour — a focused primary button currently shows nothing. Renamed to `borderFocus` for clarity of role. |
| C-4 | `density.stickyHeaderHeight` | `44px` | `44px` default, `36px` compact | Not a change of the default but a third density-dependent value, plus a second token for the two-tier header financial reports need (§4.2). |
| C-5 | `type.moneySize` | `0.9375rem` (15px) | `14px` with `tnum` | 14px × 1.210 Inter box = 17px ink, inside a 32px compact row with 4px padding. 15px forced the row taller or the leading tighter than the floor. Stated as px with the unit-bearing convention. |
| C-6 | `type.titleSize` | `1.125rem` (18px) | `20px` | One page title at 20/600; a section title at 15/600. The gap between them was 4px, which reads as two weights of the same thing. |

### Tokens to add

| ID | Token | Value |
|---|---|---|
| A-1 | `color.surfaceBand` | `#eaecee` |
| A-2 | `color.surfaceSelected` | `#ebf8ff` |
| A-3 | `color.textTertiary` | `#616b7a` |
| A-4 | `color.chromeInk` | `#545454` |
| A-5 | `color.accentBrand` / `color.accentBrandDeep` | `#0057d2` / `#0040b0` |
| A-6 | `color.actionNeutralHover` / `color.actionNeutralPress` | `#26324b` / `#1b2537` |
| A-7 | `color.stateInfoSoft` | `#eaf2fb` |
| A-8 | `color.seriesPrimary` / `seriesMid` / `seriesSoft` / `seriesNeutral` | `#1d5fbf` / `#3d7bd0` / `#c7dcf3` / `#b6bec9` |
| A-9 | `radiusPx.none/small/medium/large` | `'0px'` / `'4px'` / `'6px'` / `'12px'` — explicit-unit siblings, mandatory for any `sx.borderRadius` |
| A-10 | `space.huge` | `'48px'` |
| A-11 | `density.tableRowMinHeightCompact/Default/Comfortable` | `'32px'` / `'40px'` / `'48px'` |
| A-12 | `density.tableRowPaddingYCompact` / `…Comfortable` | `'4px'` / `'12px'` |
| A-13 | `density.cellPaddingXCompact/Default/Comfortable` | `'8px'` / `'12px'` / `'16px'` |
| A-14 | `elevation.level1` / `level2` | see §6.2; `level1` is a border string, `level2` carries the only `rgba()` in the file |
| A-15 | `layout.reportMaxWidth` / `railWidth` / `railCollapsedWidth` / `topBarHeight` / `wizardWidth` / `moneyColumnMinWidth` / `identityStripMinWidth` | see §4.3 |
| A-16 | `type.*` role names as in §3.2 | so a money role cannot be "forgotten" by a component |
| A-17 | `border.row` / `border.control` / `border.reportBlock` | 1px strings composed from the colours above, so a thickness is a token too |
| A-18 | `density.stickyHeaderHeightTwoTier` | `'72px'` / `'88px'` — the two-tier header offset pair (§4.2) |

### Non-token work this spec depends on

| ID | Item |
|---|---|
| T-1 | A money formatter under `src/format/` taking `(value, tenantCurrency, groupingStyle, locale)`. It must pass `numberingSystem: 'latn'`, `minimumFractionDigits: 2`, `maximumFractionDigits: 2`. **No currency code or BCP-47 tag may be a literal inside it** — L13 flags those anywhere outside `src/i18n/`, so the grouping-style → locale-tag map lives in `src/i18n/` (or the ESLint `allow` list gains the formatter's path, which is the integrator's call, not the designer's). |
| T-2 | `Tenant` needs a numbering-grouping setting (`western` \| `south-asian`). It does not exist today; `MASTER_PLAN.md` §4.1 requires it to be tenant data and not inferred from the browser. |
| T-3 | `AppFonts.ts` must load a weight range that actually includes **350 and 450**, or §3.2's two sub-400 roles collapse to 400 and the ladder loses the distinction SAP bought its weight axis for. |
| T-4 | `AppTheme.ts` needs the rail layout, the sticky-header treatment, the focus ring in §7, and `shape.borderRadius` kept numeric while components switch to `radiusPx.*`. |
| T-5 | `globals.css`: the `.tnum` helper becomes owned by the `type.money`/`type.code` roles; Bangla document-level leading is applied by locale; the print block in §8 is added. `globals.css` may not contain colour literals, so it references custom properties only. |
| T-6 | Message keys for every new state string in `SCREENS.md` — added to **both** `src/i18n/messages/en.ts` and `bn.ts`, since `Messages` is typed from `en` and `bn: Messages` will not compile on a missing key. That is the L17 safety net and it should stay a compile error, not a warning. |
| T-7 | `impeccable`'s context loader resolves `PRODUCT.md` at the **project root**, and this project's brief assigns it to `design/`. The integrator should place or copy `design/PRODUCT.md` to `PRODUCT.md` so future `impeccable` runs find product truth instead of re-interviewing; `design/` remains the authored copy. |
