# 03 — Ledger kernel (`src/Ledger` + `src/Data/Poster.cs`)

The heart of the product. Pure domain types in `Ledger`; the write path implemented
in `Data` because every step is a storage step.

## Entities

| Entity | File | Notes |
|---|---|---|
| `ChartOfAccount` | `Ledger/ChartOfAccount.cs` | `Code` (tenant-unique, human-facing — never a GUID), `Name`/`NameBangla`, `Type`, `SubType`, `ParentId` + materialised `ltree` `Path`, `IsPostable` (leaves only), `IsActive` |
| `Journal` | `Ledger/Journal.cs` | Posting channel owning a **gapless number sequence**: `Code`, `NumberPrefix`, `NextNumber` (read under row lock), default Dr/Cr accounts, `AllowsDrafts`, `UsesStorno` (carried, not yet honored — see gaps) |
| `GlEntry` | `Ledger/GlEntry.cs` | Header: gapless `Number`, `JournalCode`, `PeriodId`, `PostingDate`, `Memo`, `SourceType`/`SourceId`/`Iteration` (idempotency key, I5), `ReversesEntryId`, `Status` (`Draft`/`Posted` only — no `Reversed` flag, no `Approved`), `CreatedByUserId`/`PostedByUserId` |
| `GlLine` | `Ledger/GlLine.cs` | `AccountId`, `LineOrder`, nullable `Debit`/`Credit` (separate columns — never a signed amount), `Currency` + `AmountInCurrency` + `ExchangeRate` + `ExchangeRateDate` (rate **snapshotted** on the line), `AnalysisId`, `RuleVersionId` |
| `FiscalPeriod` | `Ledger/FiscalPeriod.cs` | `Number` 1–12, `FiscalYear`, `StartDate`/`EndDate`, three locks: `SoftLockedOn`, `HardLockedOn`, `TaxLockedOn` (Odoo's three tiers); `AcceptsPosting(date)` evaluated inside the posting tx |
| `ExchangeRate` | `Ledger/ExchangeRate.cs` | Effective-dated (`ValidFrom`), append-only, unique per `(tenant, currency, valid_from)`; `Rate` = functional units per 1 unit of currency; `Source` required (a rate with no source is unfalsifiable) |

## Account model

- `AccountType` (Asset/Liability/Equity/Income/Expense) → `NormalBalanceOf.For()` gives the
  normal side; `NormalBalanceOf.Balance(debit, credit, type)` produces a signed balance —
  the **single** definition (legacy had three disagreeing ones).
- `AccountSubType` (Cash, Bank, Receivable, Inventory, FixedAsset, Prepayment, Payable,
  TaxPayable, Loan, Accrual, DeferredIncome, OwnersCapital, RetainedEarnings, Drawings,
  SalesRevenue, ServiceRevenue, OtherIncome, CostOfGoodsSold, PayrollExpense,
  RentExpense, UtilityExpense, OperatingExpense, FinanceCost) — explicit column,
  **immutable once posted to** (legacy flipped `AccountType` on live accounts and
  silently inverted history; legacy also sniffed `accountName.Contains("cash")`).
- Hierarchy: adjacency (`ParentId`) + `ltree` `Path`, GiST-indexed. Parents are
  **non-postable** (`CK_accounts_postable_is_leaf`), enforced in DB.

Templates (provisioning input, not entities — editing them can't rewrite live charts):
- `BaseChartOfAccounts.Accounts` — 9 group accounts + ~20 leaves with Bangla names;
  codes `3010` Owners Capital (opening-balance plug) and `7020` Rounding Difference.
- `StandardJournals.Journals` — GL, SALE(INV), PUR(BILL), CASH, BANK(BNK), OPEN.

## Posting pipeline (`Data/Poster.cs`, 621 lines)

`IPoster.PostAsync(PostingRequest)` — order matters:

1. **Tenant required** — throws if unresolved (wiring bug, not user error).
2. **`ResolveRatesAsync`** — for foreign-currency lines (`line.Currency != Money.Currency`):
   resolve rate from `IExchangeRates.FindAsync` (newest `ValidFrom <= postingDate`) when
   caller left `ExchangeRate` null; then verify `AmountInCurrency × rate` rounds to the
   stated functional `Money` — mismatch → `ExchangeRateAmountMismatch` (I7: the FX
   difference must exist in an account, not vanish).
3. **`PostingBalance.Check`** — app-side mirror of DB rules, reports **all** errors at
   once: `NoLines`, `SingleSided`, `LineSides`, `ZeroAmount`, `Unbalanced`,
   `UnbalancedInCurrency` (per-currency totals must offset too), `MixedFunctionalCurrency`,
   `InvalidExchangeRate`, `MissingExchangeRate`. **No tolerance** — decimal equality is exact.
4. **Journal lookup** by `Code` (`JournalNotFound`).
5. **`RefuseUnpostableAccountsAsync`** — one query for all account ids; each must exist
   (tenant filter makes foreign ids invisible → same "not found" answer) and be
   `IsPostable && IsActive` (`AccountUnpostable`).
6. **`FindOpenPeriodAsync`** — period covering `PostingDate` (`PeriodNotFound`), then
   `AcceptsPosting` → `PeriodLocked` naming the lock date.
7. **`FindExistingAsync`** — idempotency: same `(SourceType, SourceId, Iteration)` →
   `AlreadyPosted` with the existing entry (audited as `ledger.posting.retry`).
8. **Transaction**: `UPDATE journals SET next_number = next_number + 1` (row lock held
   to commit — gapless, I4), reload → `NextNumber - 1`, build `GlEntry` as **Draft** +
   lines, `SaveChanges`, flip `Status = Posted`, append audit row in the **same commit**,
   save + commit.
9. **DB refusal path** — `PostgresException` SQLSTATE `P0001` (every `RAISE EXCEPTION` in
   the schema) → rollback, `ChangeTracker.Clear()`, return `RefusedByDatabase` with root
   message + audit of the refusal. Narrow on purpose: a lost connection is not a refused
   posting.

`IPoster.ReverseAsync(entryId, date, memo)` — the only correction path (I3): original
must be `Posted` (`EntryNotPosted`), not already reversed (`AlreadyReversed`, backed by
partial unique index `UQ_gl_entries_one_reversal`), then posts a `PostingRequest` with
`SourceType="ledger.reversal"`, `SourceId=original.Id`, `ReversesEntryId`, and **mirrored
lines** (Dr↔Cr swapped, everything else carried verbatim — account, currency, amounts,
rate). Actor = current actor, falling back to original's `PostedByUserId`/`CreatedByUserId`.

## Invariants → enforcement map

| # | Rule | Where enforced |
|---|------|----------------|
| I1 | Σdebit = Σcredit per entry, no tolerance | `CK_gl_entry_balanced_on_line` + `CK_gl_entry_balanced_on_entry` — `DEFERRABLE INITIALLY DEFERRED` constraint triggers on `gl_lines` (I/U/D) and `gl_entries` (status update); `assert_entry_balanced()` re-aggregates siblings at commit |
| I2 | debit XOR credit, both > 0 | `CK_gl_line_debit_xor_credit`, `CK_gl_line_currency_amount` (`amount_in_currency <> 0`), `CK_gl_line_rate_positive` |
| I3 | Posted ⇒ immutable; corrections are linked reversals | `reject_posted_entry_mutation()` (BEFORE U/D on entries), `reject_posted_line_mutation()` (BEFORE I/U/D on lines — covers smuggled lines into posted entries), `assert_reversed_entry_is_posted()` + `UQ_gl_entries_one_reversal`. **Not** `REVOKE` — Draft→Posted is itself an UPDATE, so triggers distinguish the one legal transition |
| I4 | Gapless monotonic numbering | `journals.next_number` incremented under row lock inside the posting tx; rollback returns the number; `UQ_gl_entries_number` on `(tenant, journal_code, period_id, number)` |
| I5 | Idempotent posting | `UQ_gl_entries_source_key` on `(tenant_id, source_type, source_id, iteration)` |
| I6 | Effective-dated period lock | `reject_locked_period()` BEFORE INSERT/UPDATE on `gl_entries` — checks hard → tax → soft locks |
| I7 | Money typed per currency, one rounding policy | `numeric(18,2)`/`(18,4)`/`(18,9)` columns (never float); `Money` rejects over-precise amounts; `Money.Round` explicit; `PostingBalance.ResidualLine` supports a header-rounding account (`7020`); rate + rate-date snapshotted per line |
| I8 | Limits are DB-side | Designed-in (stock/credit constraints land with P5/P6); `CreditLimitRule` in Partners is the app-side mirror |

Triggers are **not `SECURITY DEFINER`** — they read `gl_entries`/`gl_lines` as the
posting role so RLS still applies (a definer function would be a cross-tenant read
discovered by whoever adds the clause).

## Fiscal calendar (`Ledger/FiscalCalendar.cs`)

Pure functions: `FiscalYearOf` (a Jan-2027 period belongs to FY2026 — the subtle bug it
prevents), `PeriodNumberOf`, `YearOf` → 12 `FiscalPeriodSpan`s with English + Bangla
names (`bn-BD` culture). Start month is a tenant setting (default 7 = Bangladesh FY);
13-period years deliberately unsupported but schema keeps `number` a FK-able reference.

## Provisioning (`Data/LedgerProvisioner.cs`)

`ILedgerProvisioner.ProvisionAsync(fiscalYear)` — idempotent per year (interrupted
onboarding re-runs safely): creates missing `FiscalPeriod`s, base `ChartOfAccount`s
(parents first — each path resolved from parent's stored path), standard `Journal`s.
Reads `FiscalYearStartMonth` from the tenant row. `IsProvisionedAsync` = exactly 12
periods exist.

## Opening balances (`Data/OpeningBalances.cs`)

`IOpeningBalances.PostAsync(fiscalYear, balances, actorUserId)` — turns a paper balance
sheet into **one** `OPEN`-journal entry at period 1 start:

- Balances signed on the account's **normal side** (`NormalBalanceOf`), negatives allowed
  (overdraft/overpaid customer are real).
- Sides derived from account type, never asked of the importer.
- **The residual posts to Owners Capital (`3010`)** — the honest version of "the sheet
  doesn't tie": the assumption is explicit and inspectable rather than refused.
- Unknown codes reported **all at once** (`UnknownAccountCodes`); duplicate codes
  refused (`DuplicateAccountCode`); zero balances skipped; all-zero → `NoLines`.
- Idempotent via `SourceType="ledger.opening"`, `SourceId=tenantId` — one opening entry
  per company; corrections = reverse + repost.

## Exchange rates (`Data/ExchangeRates.cs`)

`IExchangeRates.FindAsync(currency, date)` → newest `ValidFrom <= date` quote or null
(distinguishes "never recorded" from "expired" — caller decides the fix).
`RecordAsync` appends only: refuses rate ≤ 0, refuses pricing the functional currency
(it's 1 by definition), refuses same `(currency, valid_from)` clash (append-only —
correct via a new effective date). Audited as `ledger.rate.record`.

## Deliberate deferrals (recorded, not bugs)

- **Storno** — `UsesStorno` carried on journal/entry; honoured nowhere. Storno needs
  negative amounts, which `CK_gl_line_debit_xor_credit` (`> 0`) refuses; weakening I2 for
  one path was rejected. Non-Storno mirror is what BD factories/accountants expect.
- **`AllowsDrafts`** — flag exists; no draft workflow exposed yet (posting is atomic
  draft→posted internally).
- **AccountResolver / effective-dated determination rules** — `RuleVersionId` column
  exists on lines; resolver lands with module posting sources (P6+).
- **`AnalysisId` dimension** — column exists; dimensional P&L is P4+.
