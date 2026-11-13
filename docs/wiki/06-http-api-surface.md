# 06 — HTTP API surface (`src/Api`)

One host, thin controllers, deny-by-default auth. Base path `/api/v1`.

## Pipeline (`Program.cs`)

```
Serilog request logging
→ HTTPS redirect (Production only; /health exempt via UseWhen)
→ CORS (configured origins only; empty = no cross-origin)
→ Authentication (JWT Bearer)
→ TenantResolutionMiddleware   ← tid claim → AmbientTenantContext; tenantless token → 403
→ Authorization (FallbackPolicy = RequireAuthenticatedUser)
→ Controllers
→ /health/live + /health/ready (anonymous, endpoint-routed not controller)
```

Startup also: strict service provider (ValidateOnBuild+Scopes), Serilog, health checks,
`AddGarmentsData` (throws without `ConnectionStrings:Default`), `AddOstagarAuth`
(JWT validation params + fallback policy), `AddGarmentsCors`, then
`PermissionSeeder.SeedCatalogueAsync` inside `TenantScope.OpenSystem`.

## Endpoints

| Method & route | Auth | Purpose |
|---|---|---|
| `POST /api/v1/auth/login` | anonymous¹ | `{tenantSlug,email,password}` → `{token, expiresAt, roles}` or 401 `{reason}` |
| `GET /api/v1/auth/me` | any authenticated | echoes resolved `userId`, `tenantId`, `displayName`, `userName` — the tenant-resolution diagnostic |
| `GET /api/v1/departments` | any authenticated | list own company's departments |
| `POST /api/v1/departments` | `Owner,Manager` | create `{code,name,nameBangla?,sortOrder?}` |
| `POST /api/v1/ledger/journals` | `Owner,Accounts` | write+post a manual journal → 201 `{entryId, number}` or 400 `{reason, errors[]}` |
| `GET /api/v1/ledger/journals?periodId=` | any authenticated | entries of one period, newest number first |
| `GET /api/v1/ledger/journals/{id}` | any authenticated | entry + ordered lines + account codes + totals + reversal links both directions |
| `POST /api/v1/ledger/journals/{id}/reverse` | `Owner,Accounts` | `{reversalDate, memo?}` → mirror entry 201 |
| `POST /api/v1/cash-book/transactions` | `Owner,Accounts` | record receipt/payment + post → 201 `{transactionId, entryId, number}` |
| `GET /api/v1/cash-book/transactions?from=&to=` | any authenticated | day book, oldest first (both dates required) |
| `GET /api/v1/cash-book/transactions/{id}` | any authenticated | one transaction with resolved codes/numbers |
| `POST /api/v1/cash-book/transactions/{id}/reverse` | `Owner,Accounts` | reverse via ledger mirror, link recorded |
| `GET /health/live`, `/health/ready` | anonymous | probes (liveness = no checks, readiness = all) |

¹ Anonymous allow-list is a **reviewed file**: `anonymous-allowlist.txt` at repo root
(`Auth/Login`, `Register`, `Refresh`, `ForgotPassword`, `ResetPassword` — only Login
exists today; the rest are forward declarations the law checks against).

## AuthN/Z details

- JWT Bearer, HmacSha256, `MapInboundClaims=false` (wire names `sub`/`tid`/`email`/`name`
  preserved — see [04](04-tenancy-identity-rbac.md)).
- Login response: `{token, expiresAt, roles[]}`; failure: 401 `{reason: AuthFailureReason}`
  — no enumeration detail.
- Role checks are `[Authorize(Roles="Owner,Accounts")]`-style on the token. The
  `(resource,action)` permission catalogue exists for granting; key-level enforcement
  handler is not yet built.
- Tenant comes **only** from the `tid` claim — never from body/query. A tenantless
  token gets 403 before reaching any handler (except `/auth/me`).

## Controller conventions

- **Thin**: no accounting rules duplicated — balance/period/account checks live in
  `PostingBalance`/`Poster`/DB; HTTP-only checks (missing fields, unparseable kind)
  return 400 `{reason}`.
- **Actor required to post**: `actor.Current?.UserId` null → 401
  `actor_required_to_post` — the ledger refuses nameless postings anyway.
- **Refusals carry everything at once**: `{reason, errors:[{code,message,line}]}` —
  an accounts clerk fixes a whole journal per submit, not one error per save.
- **Foreign resources are absent, not forbidden**: tenant query filters make other
  companies' rows invisible → 404/400 `*_not_found`, never "exists but not yours".
- **Functional currency resolved per request** from `tenants.functional_currency_code`;
  manual journals currently build domestic-currency lines only (foreign-currency
  pricing path exists in the poster but isn't exposed by this DTO yet — documented in
  the controller's remarks).
- Manual journal uses `SourceType="manual.journal"`, `SourceId=null` → **no
  idempotency** (deliberate: each POST is a distinct human act; document retries are
  the source-document modules' job).

## Cash book flow (the interesting one)

`POST /cash-book/transactions` ordering is deliberate — **posting precedes
document persistence**:

1. Validate actor → parse `kind` (`receipt`/`payment`) → load cash account (subtype
   decides journal) → resolve currency (request or tenant functional).
2. `CashTransactionValidator.Validate` (doc rules: distinct accounts, positive amount).
3. `CashPosting.For(transaction, subType)` → 2-line `PostingRequest`
   (`cash.receipt`/`cash.payment` source types; CASH or BANK journal).
4. `IPoster.PostAsync` — the ledger decides balance/period/numbering.
5. Only on success: `transaction.PostedEntryId = outcome.EntryId`, save document.

A retry after crash re-presents the same `transaction.Id` as source key → poster
answers `AlreadyPosted` → document still lands exactly once. Reverse: validator's
`CanReverse` (posted, not already reversed) → `poster.ReverseAsync` → document status
`Reversed` + `ReversedByEntryId`.

## DTOs

Request/response records live beside controllers (`CreateJournalRequest`,
`JournalLineRequest/Response`, `JournalEntryResponse`, `CreateCashTransactionRequest`,
`CashTransactionResponse`, `ReverseJournalRequest`, `CreateDepartmentRequest`) —
one type per file (L1/L1b).

## Health

`/health/live` (predicate `_ => false` — always 200 if process up),
`/health/ready` (all checks). Both `[AllowAnonymous]` via endpoint routing, not
controllers — the anonymous surface is exactly: health probes + auth login.
