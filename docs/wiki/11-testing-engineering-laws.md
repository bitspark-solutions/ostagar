# 11 — Testing and engineering laws

The project's defining feature: **checks that cannot lie about state**. Every rule
in `docs/MASTER_PLAN.md` §3 has a checker; a rule with no checker is a preference.

## The gate: `verify-all.sh`

Single command deciding whether a phase is done (`make verify`; `--quick` skips
container-backed stages). Prints PASS/FAIL with evidence plus **coverage counts**
(controllers, endpoints, migrations, constraints, source/test files) — a green gate
over an empty set reads as empty, not done.

Stages, in order:

1. `dotnet restore` + `dotnet build` (warnings are errors)
2. **Architecture laws** — `tests/Ostagar.Tests.Architecture`
3. **Unit tests** — `tests/Ostagar.Tests.Unit`
4. **Suite-membership check** — every `tests/*/*.csproj` must be listed in
   `Ostagar.slnx` (an unlisted project builds nothing and `dotnet test --no-build`
   passes over zero tests — the exact silent failure this catches)
5. **Module suites discovered, not listed** — `find tests -mindepth 2 -name '*.csproj'`
   minus the three core projects; a new module test project can't be forgotten
6. **Integration suite** — needs live Postgres: exports
   `ConnectionStrings__Default` from `.env`, runs `dotnet ef database update`
   against the dev DB first (stale-schema failures read as driver bugs otherwise)
7. **Boot check** — runs the API on an **ephemeral port** (`--urls http://127.0.0.1:0`,
   reads back the bound port — Hyper-V reserves port ranges so hardcoded ports
   eventually fail environmentally), asserts `/health/live` + `/health/ready`.
   "Building is not starting" — this catches commented-out DI registrations.
8. **Frontend** — `npm ci`, `type-check`, `lint`, `build`, `check-route-graph.mjs`,
   `jest --ci`
9. **Migrations on empty Postgres** (skipped in `--quick` or without Docker) —
   throwaway `postgres:17-alpine` container on a random port, `dotnet ef database
   update`, then `infra/sql/assert-row-level-security.sql` via psql stdin
   (`ON_ERROR_STOP`) — proves schema applies AND RLS is actually enforced
10. Coverage counts + verdict

`step_tests` wrapper: parses test totals and **fails if 0 tests ran** — the vacuity
guard.

## Engineering laws (`tests/Ostagar.Tests.Architecture`)

Registry: `EngineeringLaws.All` — 7 law classes:

| Law | Enforces |
|---|---|
| `OneTypePerFileLaw` (L1) | ≤1 top-level type per file; no nested visible types/enums |
| `FileNameMatchesDeclaredTypeLaw` (L1b) | filename == declared type; aggregate stems (`*Dtos`, `*Models`, `*Types`, `*Enums`, `*Common`, `*Helpers`…) fail outright |
| `NoCommentedOutRegistrationLaw` (L2) | no commented-out DI/service registrations |
| `NoFakeSuccessLaw` (L3) | bans `Guid.NewGuid()` as returned id, `NotImplementedException` behind 200, `TODO` in controller paths |
| `ProductionProjectPurityLaw` (L6) | test packages never in production csproj; tests compile in build |
| `ExplicitAuthorizationLaw` (L8) | every endpoint has explicit auth; anonymous only via `anonymous-allowlist.txt` |
| `PersistenceBelongsToOneProjectLaw` (L18) | no domain project references `Ostagar.Data`, takes EF packages, or writes EF usings |

Laws L4/L5/L7/L9–L17 are enforced elsewhere: DB constraints (L4/L5), the
migrations-on-empty-DB stage (L7), `TenantIsolationTests` over real HTTP (L9),
cache-key/tenant review + code conventions (L10), analyzers (L11), ESLint
`no-restricted-imports` (L12), CI greps for `Math.abs`/mock endpoints (L13),
`check-route-graph.mjs` (L14), token-only styles + CI grep (L15), reachability
reports (L16), i18n ESLint + untranslated-key check (L17).

**`LawFirepowerTests`** — the meta-law: writes deliberately non-compliant files
under `src/`, re-runs every law, asserts each **fires**, then cleans up and
verifies no residue. Found a real harness bug on first run: `SourceTree` cached
its file list, blinding every law to new files — a checker that can't see a new
file can't see a new breach. A law without a firepower probe is not a law.

`SourceTree` — the file inventory laws scan; `EngineeringLaw`/`Violation` — the
law contract (id, description, scan → violations).

## Test projects

| Project | Contents |
|---|---|
| `Ostagar.Tests.Unit` | `MoneyTests`, `CurrencyCode`/`TenantId` tests, `FiscalCalendarTests`, `NormalBalanceOfTests`, `PostingBalanceTests` + **`PostingBalancePropertyTests`** (FsCheck generative proof: balancer accepts exactly when lines balance) |
| `Ostagar.Tests.Integration` | `WebAppFactory` (boots real `Program`), `LedgerHarness`/`LedgerWorld`/`HarnessCompany` (provisions tenants+books), `TenantIsolationTests` (cross-tenant HTTP probes), `LedgerInvariantTests` (72 invalid postings → assert which invariant refused), `PosterTests`, `ReversalTests`, `OpeningBalanceTests`, `ExchangeRateTests`, `LedgerProvisioningTests`, `AuditTrailTests`, `CashBookEndpointTests`, `LedgerJournalEndpointTests`, `CompositionRootTests` |
| `Ostagar.Tests.Architecture` | the laws + firepower (above) |
| `Ostagar.Tests.Modules.CashBook` | posting shape, validation, reversal |
| `Ostagar.Tests.Modules.DocumentWorkflow` | engine transitions, allocator, gap inspector, scheme validator, print readiness, standard workflows |
| `Ostagar.Tests.Modules.Partners` | 12 files: credit, dual-role, duplicates, codes, deactivation, names, terms, price lists |

Frontend: 25 jest files under `web/__tests__` (i18n, api client, auth, components).

Integration tests run against real Postgres (CI service container or dev DB on
6432) — no in-memory fakes for the things that matter. Testcontainers.PostgreSql
is available in the package set.

## CI (`.github/workflows/ci.yml`)

Three jobs on push/PR to master:

- **backend** — postgres:17-alpine service → restore → build → architecture laws →
  unit tests → `dotnet run --project src/Cli -- db migrate` (CI migrates through
  the CLI — the deploy container's own command, not a second host-only route) →
  integration tests with `ConnectionStrings__Default` exported.
- **migrations-on-empty-database** — needs backend; fresh postgres service →
  `db migrate` on empty schema (L7).
- **frontend** — guarded on `web/package.json` existing → npm ci → type-check →
  lint → build → route-graph → jest.

## Conventions the tests enforce culturally

- A test that ran 0 tests is a failure, not a pass.
- "Did it persist?" assertions on write endpoints (L3) — integration tests verify
  rows exist, not just 200s.
- Tenant isolation proven through real HTTP with two seeded tenants — not just
  unit-level filter checks.
- Known harness pitfall documented: returning a task from a scope-opening method
  disposes the context mid-query ("BindComplete while expecting
  ReadyForQueryMessage") — the rule is written on the harness.
