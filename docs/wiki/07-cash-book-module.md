# 07 — Cash book module (`src/Modules/CashBook`)

The daily cash book — receipts and payments that post **through** the ledger engine,
never beside it. This is the rebuild of the legacy cash book whose single-sided
`Posted` entries made the trial balance unbalancedable.

## Domain model (11 files, all pure — no EF, no HTTP)

| Type | Role |
|---|---|
| `CashTransaction` | The document: `Kind` (Receipt/Payment), `Status` (Posted/Reversed), `TransactionDate`, `CashAccountId`, `CounterAccountId`, `Amount`+`Currency`, `PartnerId?`, `Memo`, `ActorUserId` (declared, not ambient), `Iteration` (idempotency), `PostedEntryId`, `ReversedByEntryId`, `CreatedAt`. `TypedAmount()` re-types to `Money` |
| `CashTransactionKind` | `Receipt` (money in) / `Payment` (money out) — decides which side the cash account lands on |
| `CashTransactionStatus` | Only `Posted` and `Reversed` — **no Draft**: a cash line is written when money moves; unposted cash rows are how legacy's book disagreed with its ledger |
| `NewCashTransaction` | Input record: kind, date, two account ids, `Money? Amount`, optional partner, memo |
| `CashTransactionValidator` | Doc-level rules only: both accounts present, distinct (`SameAccountBothSides`), amount present + positive (`AmountNotPositive` — direction comes from Kind, not sign). `CanReverse()`: must have `PostedEntryId`, not already reversed |
| `CashPosting` | `For(transaction, cashAccountSubType)` → `CashPostingPlan`: picks journal by subtype (`Cash`→`CASH`, `Bank`→`BANK`, else `CashAccountNotCashLike`), builds the 2-line `PostingRequest` |
| `CashPostingPlan` | `Ready(request)` or `Refused(errors)` |
| `CashSourceTypes` | `"cash.receipt"` / `"cash.payment"` — half of the I5 idempotency key; constants so a typo can't silently double-post |
| `CashTransactionError(Code, Message)` + `CashTransactionErrorCode` | `AccountMissing`, `SameAccountBothSides`, `AmountMissing`, `AmountNotPositive`, `CashAccountNotCashLike`, `TransactionNotPosted`, `TransactionAlreadyReversed` |
| `CashTransactionDecision` | `Ok()` / `Refused(errors)` result type |

## The posting shape

Two lines, always balanced by construction:

- **Receipt**: Dr cash account / Cr counter account
- **Payment**: Dr counter account / Cr cash account

`SourceType` = `cash.receipt`/`cash.payment`, `SourceId` = `transaction.Id`,
`Iteration` = `transaction.Iteration`, `ActorUserId` = declared actor.
Journal = `CASH` or `BANK` by the cash account's `AccountSubType` — two journals
because each owns a gapless sequence and owners ask "where is CASH-000041" on paper.

Everything beyond the two-line shape — balance check, period locks, account
postability, rate resolution, gapless numbering, immutability — is `IPoster`'s job.
The module deliberately cannot become a second, weaker posting engine.

## Persistence (`cash_transactions` table)

`CashBookModelConfiguration`: amount `numeric(18,4) > 0`, `cash_account_id <>
counter_account_id`, FKs to `accounts` (both sides, Restrict) and to `gl_entries`
(`PostedEntryId`, `ReversedByEntryId`, Restrict — a document can't outlive the entry
that proves it), index `(tenant_id, transaction_date)` for the day book.

## HTTP surface (`CashBookController`)

See [06](06-http-api-surface.md). Notable: **posting precedes document save** — the
ledger request carries the document id as source key, so the entry exists before the
document row claims it; a ledger refusal leaves no document claiming unposted money.
Retry-safe via I5. List endpoint requires both `from`/`to` dates, returns oldest-first
(day-book order). Responses resolve account codes and entry numbers for display.

## Tests (`tests/Ostagar.Tests.Modules.CashBook`)

`CashPostingTests` (journal selection, line shape), `CashTransactionValidationTests`
(document rules), `CashTransactionReversalTests` (once-only reversal). Endpoint
behavior covered by `CashBookEndpointTests` in the integration suite.

## What it doesn't do yet (P3 remainder)

- No CSV import of the client cash-book format (the P3 gate item).
- No bank accounts/transfers/reconciliation (BANK journal exists, unused).
- `PartnerId` is stored but partners module isn't wired to posting sources yet.
- No UI — API only; web has no cash-book screens yet.
