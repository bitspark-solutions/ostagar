# 02 — Architecture overview

## Solution shape

`Ostagar.slnx` (XML solution format, .NET 10) — a **modular monolith**: one deployable
API host, one CLI host, shared class libraries. No microservices, no message bus yet.

```
src/
  Core/        primitives: Money, Currency, CurrencyCode, TenantId, ITenantScoped
  Ledger/      kernel entities + pure rules: CoA, journals, GL, posting contracts,
               fiscal calendar, exchange rates, opening balances, base templates
  Data/        OstagarDbContext, migrations, EF mapping, interceptors, Poster,
               provisioner, opening balances, exchange-rate store, audit writer,
               permission seeder, tenant directory, RLS probe, migration runner
  Platform/    tenancy context + directory entities, identity users/roles, JWT issuer,
               RBAC catalogue/grants, audit model, current-user/actor readers.
               ZERO EF references (law L18).
  Modules/
    CashBook/          receipt/payment documents → posting plans (live)
    DocumentWorkflow/  state machines + document numbering + print readiness (live, domain-only)
    Partners/          partner aggregate: codes, identifiers, credit, duplicates (live, domain-only)
    Inventory/ Purchasing/ Sales/ Payroll/ Production/   empty csproj shells (P5–P7b)
  Api/          composition root: Program.cs, middleware, controllers, health, auth wiring
  Cli/          offline commands: `db migrate`, `superuser create`
tests/          Architecture (laws), Unit, Integration, per-module suites
web/            Next.js 16 + React 19 + MUI 7 frontend
design/         DESIGN.md token source + mocks
infra/          compose.dev.yml, hardened image wrappers, RLS assertion SQL
legacy/         frozen predecessor (tag legacy-final), read-only reference
```

## The layering law that matters most (L18 / §2.7)

Persistence knowledge lives in **exactly one project: `src/Data`**. The dependency
arrow is:

```
Core ← Ledger ← Platform(entities only, no EF) ← Data ← Api/Cli (hosts)
```

- `Platform` compiles with **no EF package at all** — `ITenantScoped` lives in `Core`
  precisely so every module can mark tenant-owned entities without depending on
  persistence.
- `Ledger` references only `Core`; `IPoster`, `IExchangeRates`, `ILedgerProvisioner`,
  `IOpeningBalances` are *declared* in Ledger and *implemented* in Data, so the domain
  never learns EF exists.
- Hosts (`Api`, `Cli`) are exempt — a composition root must register a context.
- Enforced by `PersistenceBelongsToOneProjectLaw` (csproj + `using` scan) with a
  firepower probe that plants a `DbSet` in a domain project.

Why not interfaces/repositories to hide the context? Recorded decision: one context,
one database, and the tenancy guarantee depends on that concrete context owning the
tenant query filters — hiding it behind an interface makes isolation easier to bypass.

## Composition root (`src/Api/Program.cs`)

Order is deliberate:

1. `UseStrictServiceProvider()` — `ValidateOnBuild` + `ValidateScopes`: the container
   proves it can construct **everything** at startup (the failure that killed legacy:
   22 commented registrations discovered only at request time).
2. Serilog request logging.
3. `AddControllers()` **before** authorization services.
4. `AddGarmentsObservability` → `AddGarmentsHealth` → `AddGarmentsData` →
   `AddOstagarAuth` → `AddGarmentsCors`.
5. Pipeline: Serilog → HTTPS redirect (prod only, `/health` exempt — a load balancer
   probing plain HTTP must get 200, not 307) → CORS → Authentication →
   **`TenantResolutionMiddleware`** → Authorization → Controllers → health endpoints.
6. `PermissionSeeder.SeedCatalogueAsync` on every boot inside `TenantScope.OpenSystem`
   (the permission catalogue is global reference data; the seeder is idempotent with
   `ON CONFLICT DO NOTHING` for parallel-boot safety).

`MigrateOnStartup` is **false** — migrations run via the one-shot `migrate` compose
service (`ostagar-cli db migrate`) before the API starts.

## The two hosts

- **`src/Api`** — the only HTTP host. Controllers are thin: they translate HTTP ↔
  domain and add no accounting rules of their own.
- **`src/Cli`** (`ostagar-cli`) — offline commands sharing the same DI registrations
  (`CliHost`), so a command cannot succeed against wiring the app never uses:
  - `db migrate` — applies pending migrations as the migration principal
  - `superuser create --tenant <slug> --email <e> [--password-env NAME]` — the **only**
    path that creates an administrator; deliberately not an HTTP endpoint.

## Key cross-cutting mechanisms

| Mechanism | Where | Purpose |
|---|---|---|
| `ITenantContext` / `AmbientTenantContext` | Platform.Tenancy | scoped tenant holder; throws when unresolved; `EnterSystemScope` for global tables |
| `TenantSessionInterceptor` | Data | sets `app.tenant_id` on every connection open; **refuses to open without a tenant** |
| `TenantSaveChangesInterceptor` | Data | stamps `tenant_id` on inserts, refuses cross-tenant mutation |
| EF global query filters | `OstagarDbContext` | every `ITenantScoped` entity filtered to `CurrentTenantId` (None → matches nothing) |
| RLS + check constraints | migrations | the database-level half of isolation |
| `IAuditWriter` → `DatabaseAuditWriter` | Platform.Audit / Data | audit rows staged into the **same unit of work** as the change — one commit or neither |
| `ICurrentUser` / `ICurrentActor` | Platform.Security | read `sub`/`tid`/`name` claims; actor identity for audit |
| `AuthClaimNames` | Platform.Security | `sub`, `tid`, `email`, `name` — one name per fact, no remapping (`MapInboundClaims=false`) |

## Module pattern (how a business module plugs in)

Modules are **pure domain libraries**: entities + rules + validators, no EF, no HTTP.
They produce `PostingRequest`s consumed by `IPoster` (implemented in Data). Examples:

- **CashBook**: `CashTransaction` document → `CashPosting.For()` builds a 2-line
  `PostingRequest` → `IPoster.PostAsync`. Journal chosen by account subtype (Cash→CASH,
  Bank→BANK).
- **DocumentWorkflow**: `DocumentWorkflowEngine.Evaluate()` decides state transitions;
  `DocumentNumberAllocator.Allocate()` is a pure function over a counter row — the
  integrator owns the one transaction.
- **Partners**: `Partner` aggregate with value objects (`PartnerCode`, `PartnerName`,
  `BanglaName`, …), `CreditLimitRule`, `DuplicateMatchEvaluator`, `PriceList`.

## Version stack (Directory.Packages.props, centrally pinned)

`net10.0` · EF Core + Npgsql EF provider 10.0.13/10.0.3 · ASP.NET Core Identity EF ·
JWT Bearer 10.0.13 · Serilog 8.0.3 · xunit 2.9.3 · FsCheck 2.16.6 · Testcontainers ·
Roslyn 5.0.0 (for law checkers) · `TreatWarningsAsErrors` + lock files everywhere.
