# 05 — Persistence and data layer (`src/Data`)

The only project allowed to know Entity Framework (law L18). Two DbContexts, eight
migrations, and the posting engine.

## Two contexts, deliberately

| Context | Purpose | Interceptors |
|---|---|---|
| `OstagarDbContext` : `IdentityDbContext<ApplicationUser, ApplicationRole, Guid>` | everything tenant-scoped: tenants directory FK targets, identity, RBAC, audit, ledger, cash book | `TenantSessionInterceptor` + `TenantSaveChangesInterceptor` |
| `TenantDirectoryDbContext` | tenant lookup only — must work **before** a tenant is known (login slug → tenant id) | none, on purpose: "not tenant-scoped" is a fact about the code, not a mode someone forgets to exit |

Both `AddDbContext`, never pooled. Registered via `AddGarmentsData(options)`
(`DataServiceCollectionExtensions`): also wires `AmbientTenantContext`/`ITenantContext`,
`ITenantDirectory→TenantDirectory`, `IPoster→Poster`, `ILedgerProvisioner→LedgerProvisioner`,
`IOpeningBalances→OpeningBalances`, `IExchangeRates→ExchangeRates`,
`IAuditWriter→DatabaseAuditWriter`, `MigrationRunner`, `RlsSafetyProbe`.

`OstagarDatabaseOptions` (section `GarmentsDatabase` — legacy config-section name kept):
`ConnectionStrings:Default` (runtime principal — must NOT be owner/superuser),
`ConnectionStrings:Migration` (optional separate migration principal),
`RequireTenantScopedPrincipal` (default true), `MigrateOnStartup` (default false).
`ReadDatabaseOptions` throws without a connection string — no invented fallback.

## Interceptors

**`TenantSessionInterceptor`** (`DbConnectionInterceptor`): on every physical
connection open → `set_config('app.tenant_id', @tenant, false)` (session-scoped, not
transaction-local — survives for the connection's life). No tenant and no system scope
→ **throw** (fail closed). This is what the RLS policies read.

**`TenantSaveChangesInterceptor`** (`SaveChangesInterceptor`): for Added/Modified/
Deleted entries with a `TenantId` property (mapped or shadow):
- `Added` → stamp session tenant (payload can't forge it).
- `Modified`/`Deleted` → compare `OriginalValue` tenant to session; mismatch → throw.
  (Read-then-write of `tenant_id` is also neutralized via `IsModified = false`.)

## EF mapping conventions

- **Snake case everywhere**: `SnakeCaseModelNames.Apply` rewrites every column name
  (`ApplicationUserId` → `application_user_id`) — shared by both contexts so they can't
  drift (RLS policies name `tenant_id` literally).
- **Enums as text** (`HasConversion<string>()`), never ordinals — legacy persisted
  ints while models read strings; schema and entity described different tables.
- **`TenantId`/`CurrencyCode` value converters**: `TenantIdConverter` (uuid ↔ wrapper,
  empty→None), `CurrencyCodeConverter` (3-letter ISO text; `CurrencyCode.From` validates
  on read — a bad row fails materialisation, not silent miscompare).
- **Money columns**: `numeric(18,2)` debit/credit, `numeric(18,4)` amount_in_currency,
  `numeric(18,9)` exchange_rate — never float.
- **Dates as `date`**, timestamps `timestamptz`.
- **Bangla collation** `bn-x-icu` declared per searchable text column
  (`UseCollation`) — dev/CI clusters init `--locale=C` for determinism; Bangla ordering
  is per-column. Verified: Postgres 17 alpine ships `bn-x-icu`.
- **Delete behavior Restrict** almost everywhere — deleting a period/journal/account/
  tenant must never cascade the ledger. Exceptions: entry→lines cascade (one whole),
  tenant FK restrict.

## `LedgerModelConfiguration` (the constraint surface)

Maps `accounts`, `journals`, `fiscal_periods`, `gl_entries`, `gl_lines`,
`exchange_rates` — see [03](03-ledger-kernel.md) for the full I1–I8 enforcement map.
Unique keys: `(tenant_id, code)` on accounts/journals, `(tenant_id, fiscal_year,
number)` periods, `UQ_gl_entries_source_key` (idempotency), `UQ_gl_entries_number`,
`UQ_gl_entries_one_reversal`, `UQ_exchange_rates_currency_valid_from`. Resolution index
`IX_exchange_rates_resolution` on `(currency, tenant_id, valid_from DESC)`.

`GlEntry`→`Journal` FK is **composite** `(TenantId, JournalCode)→(TenantId, Code)` —
a bare code FK would let tenant A's journal satisfy tenant B's posting.

## `CashBookModelConfiguration`

`cash_transactions`: kind/status as text, `amount numeric(18,4) > 0`,
`cash_account_id <> counter_account_id`, FKs to accounts (Restrict) and to `gl_entries`
for `PostedEntryId`/`ReversedByEntryId` (Restrict — document can't outlive entry claims),
index `(tenant_id, transaction_date)` for the day book.

## Migrations (8, in order)

1. `InitialTenantSchema` — tenants, identity, RBAC, audit, company profiles, departments.
2. `EnableRowLevelSecurity` — roles `garments_owner`/`garments_app` (legacy DB role
   names kept), FORCE RLS + `tenant_isolation` policies.
3. `ScopeIdentityIndexesToTenant` — the global-unique-index fix.
4. `AddLedgerSchema` — CoA/journals/periods/GL/ltree.
5. `AddLedgerEnforcement` — triggers for I1/I3/I6 + RLS on the 5 ledger tables.
6. `MakeAuditTrailAppendOnly` — revoke UPDATE/DELETE on `audit_logs`.
7. `AddExchangeRates` — rate history table.
8. `AddCashTransactions` — cash book document table.

Migration runner: `MigrationRunner.ApplyPendingAsync` builds a context **without**
tenant interceptors (DDL isn't tenant-scoped) using the migration connection string.
`OstagarDbContextFactory` (design-time): reads `ConnectionStrings__Default` env var,
enters system scope — `dotnet ef` can't work through the tenant guard, and relaxing
the guard for tooling was rejected. In deploy/CI, migrations run via
`ostagar-cli db migrate` — the same path production takes.

## Services in Data (implemented here, declared in Ledger/Platform)

- **`Poster`** — the posting engine; see [03](03-ledger-kernel.md).
- **`LedgerProvisioner`** — idempotent per fiscal year: periods → accounts (parents
  first, paths from stored parent paths) → journals.
- **`OpeningBalances`** — paper balance sheet → one balanced `OPEN` entry, residual to
  Owners Capital; see [03](03-ledger-kernel.md).
- **`ExchangeRates`** — effective-dated append-only rate store + audit.
- **`TenantDirectory`/`TenantDirectoryDbContext`** — slug↔tenant resolution.
- **`PermissionSeeder`** — global `resource_actions` catalogue (`ON CONFLICT DO
  NOTHING` for parallel boots) + per-tenant system roles.
- **`DatabaseAuditWriter`** — stages `AuditLogEntry` into ambient context's unit of work.
- **`RlsSafetyProbe`** — startup verification the runtime principal can't bypass RLS and
  every tenant table is actually protected (see [04](04-tenancy-identity-rbac.md)).

## Conventions worth knowing

- All identifiers `uuid` (`Guid`), display codes are strings, money `numeric`,
  enums `text`, dates `date`.
- `tenant_id` leads composite indexes; `ResourceAction.Id` is `int` (global catalogue).
- `audit_logs.id` is `bigint` sequence.
- Bangla text columns use `bn-x-icu`; identifiers/codes stay deterministic byte order.
- `next_number` on `journals` is the gapless counter (per journal, not per period —
  see gaps page).
