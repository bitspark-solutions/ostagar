# 09 — Partners module (`src/Modules/Partners`)

The largest domain module so far (45 files, ~4,000 LOC): who the factory buys from
and sells to. Pure domain — entities, value objects, rules; persistence/integration
lands with P6.

## Central design decision: one aggregate, not two tables

`Partner` (985 lines — the biggest file in the codebase, justified by the domain
density) is a **single identity that may hold two capabilities**: `CustomerProfile`
and `VendorProfile` hang off it. Rationale: in garment trade the same party often
buys output AND supplies input (a trading house places export orders and releases
knit fabric). Two records would split receivable/payable across two ids, understate
aging on both sides, and hide that they're the same company.

## Identity model

- `Id` (`PartnerId`) + `Code` (`PartnerCode`) minted **once** at registration —
  never derived from names, never reassigned, immutable forever. Renames keep every
  document/ledger/price-list reference intact.
- Three name fields, three facts: `LegalName` (`PartnerName`), `TradingName?`,
  `BanglaName?` (`BanglaName` — separate so Latin legal names stay intact for
  banks/LCs).
- `PartnerIdentificationBasis` — the evidence the record was opened on; fixed at
  registration; `IsIdentityVerified` reads the facts.
- `Status`: `Active` / deactivated (`InactiveOn`) — deactivation refused while open
  documents exist (probe via `IOpenDocumentProbe`).

## Supporting types (~44 files)

- **Identifiers**: `PartnerIdentifier` + `PartnerIdentifierKind` (TIN, BIN, trade
  licence, bank account…) + `IdentifierAuthority` + `PartnerIdentificationBasis` +
  `ExistingPartnerIdentifier` — identifiers validated into their register's form;
  minimum count required by the declared basis.
- **Contact surface**: `PartnerAddress` (+`AddressKind`), `PartnerContactPoint`
  (+`ContactKind`), `PartnerBankAccount`.
- **Money terms**: `PaymentTerm` + `PaymentTermKind` (advance/net-30/days…),
  `PriceList` + `PriceListLine` (effective-dated price lists per currency/document
  currency; `IStyleCatalog` resolves style references — prices reference catalogued
  styles, never free text).
- **Ledger link**: `LedgerAccountReference` — a partner's AR/AP account is a
  reference into the chart with a declared normal side.
- **Duplicate detection**: `DuplicateMatchEvaluator` + `DuplicateMatch`/
  `DuplicateMatchReport`/`DuplicateVerdict` + `IPartnerDuplicateDirectory` —
  detects likely-same-party registrations before they split the books.
- **Codes**: `IPartnerCodeAllocator` — sequential partner codes (never random,
  never name-derived).
- **Rules/text**: `PartnerTextRules`, `PartnerVocabulary`, `PartnerRuleException`,
  `PartnerErrorCode`, `PartnerStatus`, `PartnerRole`.

## Credit control (`CreditLimitRule`)

`Evaluate(creditLimit, currentExposure, proposedOrder)` → `CreditDecision`:

- No limit → `CreditLimitNotSet` (refused, not assumed unlimited).
- Currency mismatch → `CurrencyMismatch`.
- Committed = exposure + order, compared **exactly** — one paisa over refuses
  (legacy's ±0.01 tolerance let receivables grow past every limit).
- Headroom/overage returned as `Money` — app-side mirror of the I8 DB constraint
  that lands with sales (P6).
- Exposure comes from the AR projection — the module keeps **no balance of its own**
  ("a second balance is a second answer").

## Partner-side capabilities

`CustomerProfile`/`VendorProfile` hold role-specific data (credit limit, price
lists, payment terms, document currencies). `NewPartner` is the registration draft;
`Partner` methods (`ChangeLegalName`, `ChangeTradingName`, `ChangeBanglaName`,
identifier/address/contact management) enforce the rules — refusal is the default:
no payment channel → no payment, no credit limit → no credit sale, no effective
price list → no price.

## Tests (`Ostagar.Tests.Modules.Partners`, 12 files)

Credit limits, dual-role partners, duplicate detection, code allocation,
deactivation, name validation, record keeping, rename, payment channels/terms,
price lists — all pure-domain unit tests.

## Status

Domain-complete and heavily tested; **not yet persisted or exposed over HTTP** —
waiting for P6 (sales/purchasing cycles) to provide documents, exposure sources
(`ICreditExposureSource`, `IOpenDocumentProbe` are the declared integration seams).
