# 12 — Infrastructure and operations

## Dev topology (`infra/compose.dev.yml`)

Five services, `docker compose --env-file .env -f infra/compose.dev.yml`
(`make up`):

| Service | Image | Host port → container | Notes |
|---|---|---|---|
| `postgres` | `ostagar-postgres:dev` (hardened wrapper) | **6432**→5432 | `POSTGRES_INITDB_ARGS=--locale=C --encoding=UTF8` (deterministic; Bangla collation is per-column); volume `pg_data` |
| `redis` | `ostagar-redis:dev` (source build) | **6380**→6379 | `--save '' --appendonly no` (cache only, tenant-keyed per L10) |
| `migrate` | `ostagar-cli:dev` | — | one-shot `db migrate`; api `depends_on: service_completed_successfully` |
| `api` | `ostagar-api:dev` | **41983**→8080 | env: connection string, `OstagarAuth__*`, `Cors__AllowedOrigins__0` |
| `web` | `ostagar-web:dev` | **42107**→3000 | build args `NEXT_PUBLIC_API_BASE_URL` (browser origin, not service name), `NEXT_PUBLIC_DEFAULT_LOCALE=bn` |

**Port rationale** (verified by real binds, not exclusion lists): 5432–5435 reserved
by Windows/Hyper-V (EACCES), 6379/3000 taken by another project, 5199 reserved,
3100 inside an exclusion range. Chosen high ports: 6432, 6380, 41983, 42107.
Env-overridable via `OSTAGAR_API_PORT`/`OSTAGAR_WEB_PORT`.

**No MailHog/pgAdmin/Redis-Commander** — dev tooling that must never appear in prod
topology (the legacy compose mixed them and called it production). `compose.prod.yml`
is a real P8 deliverable, not yet written.

## Hardened base images (`infra/Dockerfile.*`)

Zero known vulnerabilities was a stated goal (commit `e8a2610`):

- **`Dockerfile.postgres`** — `postgres:17-alpine3.23` + `apk upgrade` + **gosu built
  from source** on patched Go (upstream's vendored gosu scored ~60 stdlib CVEs;
  Alpine's package was no better). Overwrite-in-place keeps SBOM truthful.
- **`Dockerfile.redis`** — Redis **7.4.12 built from source** on Alpine 3.23 (every
  published `redis:7.x-alpine` tag tracks Alpine 3.21 with unfixable busybox/util-linux
  CVEs). Official entrypoint reused; `setpriv` path bridged.
- **`src/Api/Dockerfile` / `src/Cli/Dockerfile`** — SDK build → `aspnet:10.0-alpine-extra`
  **musl** runtime (glibc CVEs eliminated by construction; musl pinned at 1.2.6-r3 —
  the edge r6 build's hardened DNS breaks Docker's embedded resolver; documented
  accepted CVE). `apk upgrade` on top. Non-root `app` user.
- **`web/Dockerfile`** — `node:26-alpine3.23` multi-stage; `npm ci`; standalone
  output; runner strips npm/corepack (the entire language-package CVE count).
  `NEXT_PUBLIC_*` are **build-time args** — inlined into the client bundle, can't be
  changed at container start (that's why compose passes the browser-facing origin).

`docker scout cves` re-scan is documented on bump for each image.

## Configuration & secrets

`.env.example` → `.env` (never committed): `POSTGRES_PASSWORD`,
`ConnectionStrings__Default` (+ optional `__Migration`), `ConnectionStrings__Redis`,
`NEXT_PUBLIC_API_BASE_URL`, `OstagarAuth__{Issuer,Audience,SigningKey,LifetimeMinutes}`,
`Cors__AllowedOrigins__*`, port overrides.

Secrets policy: JWT signing key ≥32 chars from env, **no default** — app refuses to
start without it (legacy committed its secret; scrubbed + burned). `appsettings.json`
carries only Serilog config. `.env` passwords treated as compromised after the
2026-10-15 history scrub → `make dev-reset` destroys the volume.

## CLI (`ostagar-cli`, `src/Cli`)

Same DI registrations as the API (`CliHost`) — a command can't succeed against
wiring the app never uses. Commands:

- `db migrate` — `MigrationRunner.ApplyPendingAsync` as migration principal; the
  deploy step (compose `migrate` service; CI uses the same command).
- `superuser create --tenant <slug> --email <e> [--password-env NAME | --password V]`
  — resolves tenant via system scope → `TenantScope.Open` → seeds tenant roles →
  creates user + `Owner` role (idempotent: existing email just gets Owner added).
  **The only path that creates administrators** — no HTTP equivalent exists
  (legacy's `POST /api/AdminSetup/create-admin` was unauthenticated).
  `--password-env` preferred (argv passwords show in process lists/shell history).

## Makefile targets

`verify` / `verify-quick` (gate) · `build` · `test` · `up`/`down`/`logs` ·
`dev-reset` (typed YES confirmation — destructive) · `migrate` (one-shot container)
· `db-shell` (psql into dev DB).

## CI (`.github/workflows/ci.yml`)

`verify` workflow, three jobs (details in [11](11-testing-engineering-laws.md)):
backend (build + laws + unit + CLI migrate + integration on service Postgres) →
migrations-on-empty-database → frontend (type-check/lint/build/route-graph/jest).

## Scripts

- `verify-all.sh` — the gate ([11](11-testing-engineering-laws.md)).
- `scripts/vul-scan.sh` — vulnerability scanning helper.
- `infra/sql/assert-row-level-security.sql` — RLS assertions run against fresh DBs.
- `web/scripts/check-route-graph.mjs` — closed-route-graph check (L14).

## Operational notes

- Migrations are a **deploy act**: `MigrateOnStartup=false`; the `migrate` container
  completes before api starts (`service_completed_successfully`).
- `RlsSafetyProbe` runs at API startup — wrong DB principal = boot failure, not a
  silent leak.
- Serilog → console (JSON-friendly envelope); `AddGarmentsObservability` wires
  `LogContext` enrichment; correlation via `TraceIdentifier` shared between audit
  rows and request logs.
- `anonymous-allowlist.txt` — the reviewed list of unauthenticated endpoints;
  editing it IS the security review.
