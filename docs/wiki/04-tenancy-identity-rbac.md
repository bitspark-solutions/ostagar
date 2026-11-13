# 04 — Tenancy, identity, RBAC, audit

## Tenant model

`Tenant` (`Platform/Tenancy/Tenant.cs`) — the directory row every company hangs off:
`Id` (`TenantId` wrapper — empty guid can never be a tenant), url-safe immutable `Slug`,
`DisplayName`, `PreferredLanguage` (default `"bn"`), `FunctionalCurrencyCode`
(default `"BDT"`), `FiscalYearStartMonth` (default 7), `IsActive`.

`ITenantScoped` (`Core`) is the marker every tenant-owned entity implements — the model
builder turns it into: `tenant_id` column + `TenantIdConverter` (uuid), FK to `tenants`
with `DeleteBehavior.Restrict` (a mistyped tenant delete can't cascade the ledger),
composite indexes leading with `tenant_id`, a `CK_<table>_tenant_scope` check constraint
(`tenant_id IS NOT NULL AND tenant_id = current_setting('app.tenant_id', true)::uuid`),
a global EF query filter, and an RLS policy in the migration.

The **tenant directory itself is not tenant-scoped** — it's what makes a tenant id
exist (login slug → tenant resolution happens before tenancy is known). Served by a
separate `TenantDirectoryDbContext` with **no interceptors**, deliberately.

`CompanyProfile` — one per tenant (unique `tenant_id`): legal/trading names (Bangla
collation), TIN/BIN, address, contacts. `Department` — factory cost centres
(cutting/sewing/finishing…), code unique per tenant.

## Triple-layer isolation (each closes a hole the others leave)

1. **Postgres RLS** — `ENABLE` + `FORCE ROW LEVEL SECURITY` + policy `tenant_isolation`
   (`USING`/`WITH CHECK tenant_id = current_setting('app.tenant_id', true)::uuid`) on
   every tenant-owned table. Stops raw SQL and non-EF readers. `FORCE` means even the
   table owner is subject.
2. **Check constraint** — same predicate as a `CHECK`, so a write under a mismatched or
   unset session errors instead of silently landing (or vanishing).
3. **EF query filters + `TenantSaveChangesInterceptor`** — filters cover `Include`,
   projections, `count`; the interceptor stamps `tenant_id` on every `Added` row
   (including Identity's shadow-property join tables) and **throws** on any
   `Modified`/`Deleted` row whose `OriginalValue` tenant ≠ session tenant.

Session side: `TenantSessionInterceptor` (`DbConnectionInterceptor`) runs
`set_config('app.tenant_id', @tenant, false)` on **every** connection open and
**refuses to open at all** without a resolved tenant or explicit system scope —
fail-closed, because a null setting against the policy reads as "empty tenant" (looks
working, is broken). `AddDbContext`, never `AddDbContextPool` (session state would leak
between requests — legacy pooled at 128).

`AmbientTenantContext` (scoped): `Set()` once per scope, throws on reassignment;
`EnterSystemScope()` for global tables (permission catalogue) — greppable via
`TenantScope.OpenSystem`, and RLS still returns nothing if tenant data is read through it.
`TenantScope.Open(scopeFactory, tenantId)` is how CLI/background work establishes tenancy.

`RlsSafetyProbe` — startup check against the live DB: runtime principal must not be
superuser/BYPASSRLS/table-owner, every model-claimed tenant table must have RLS enabled
+ forced + the `tenant_isolation` policy, and no table may be missing. Turns "policies
look right in the migration" into a boot failure if the app role is wrong.

`infra/sql/assert-row-level-security.sql` — same assertions for fresh-migrated DBs in
the gate/CI (policy count == count of tables carrying `tenant_id`, `garments_app` can't
bypass).

## Identity & sign-in

ASP.NET Core Identity EF stores, tenant-scoped:

- `ApplicationUser : IdentityUser<Guid>, ITenantScoped` — `FullName`, `IsActive`.
  Identity's **global** unique indexes on `NormalizedUserName`/`NormalizedEmail` are
  replaced by non-unique globals + unique `(TenantId, NormalizedUserName)` — otherwise
  tenant B's "owner@factory.com" collides with tenant A's and the error itself leaks
  that the address exists elsewhere.
- `ApplicationRole : IdentityRole<Guid>, ITenantScoped` — `DisplayName`, `IsSystem`
  (system roles can't be deleted; only grants re-granted). Same global-unique-index fix.
- Join tables (`user_roles`, `user_claims`, `user_logins`, `user_tokens`, `role_claims`)
  get **shadow** `TenantId` — RLS + check + index, but **no query filter** (Identity's
  cascade deletes would fail on filtered rows).

`AuthService.SignInAsync(LoginRequest{TenantSlug, Email, Password})`:

1. Resolve tenant via `ITenantDirectory.FindBySlugAsync` (unscoped context).
2. Unknown/inactive tenant → `TenantUnavailable` (same answer whether slug is wrong or
   company closed — no customer enumeration).
3. `TenantScope.Open(tenant.Id)` → `UserManager.FindByEmailAsync` + `CheckPasswordAsync`
   **inside** the tenant scope, so the lookup already runs under RLS.
4. Unknown user / wrong password → `InvalidCredentials` (one reason for all failures);
   `!IsActive` → `AccountDisabled`.
5. `AccessTokenIssuer.Issue(user, tenant.Id, roles)` → JWT (HmacSha256, 30 min default).

`AccessTokenOptions` — `OstagarAuth:{Issuer,Audience,SigningKey,LifetimeMinutes}`;
`Validate()` demands ≥32-char key, no default (legacy committed its JWT secret; it's
burned). Claims: `sub` (user id), `tid` (tenant id), `jti`, `email`, `name`, `role`.

JwtBearer config: **`MapInboundClaims = false`** — without it `sub`/`tid` arrive
rewritten as `…/claims/nameidentifier`/`…/claims/tenantid` and every `AuthClaimNames`
reader silently finds nothing (authenticated but tenantless → 403 everywhere). This was
a real P1 defect. `ClockSkew` 30s (legacy had `false` = no skew tolerance… actually
legacy set `false` meaning zero tolerance; new code allows 30s).

`AuthClaimNames`: `sub`, `tid`, `email`, `name` — one name per fact.
`CurrentUserService`/`CurrentActorService` read claims via `IHttpContextAccessor`;
`SourceAddress` = `RemoteIpAddress` (deliberately **not** `X-Forwarded-For` — client-
spoofable; legacy trusted it). `CorrelationId` = `TraceIdentifier` (null outside HTTP,
never a fabricated guid).

## Authorization

- `FallbackPolicy = RequireAuthenticatedUser` — deny by default; public is opt-out per
  route (`[AllowAnonymous]`), never opt-in. Legacy had bare `AddAuthorization()` →
  everything without an attribute was public, including admin creation.
- Controllers use `[Authorize]` and `[Authorize(Roles = "Owner,Accounts")]` etc.
- **`PermissionCatalogue`** — global `(resource, action)` vocabulary
  (`tenant.read`, `journal.post`, `rate.record`, `ledger.provision`, … ~30 keys), seeded
  into `resource_actions` on every boot by `PermissionSeeder` (global table, no tenant).
- **`DefaultRoleMatrix`** — 5 system roles per tenant: `Owner` (`*.*` wildcard),
  `Manager`, `Accounts` (ledger keys: journal.read/create/post/reverse/open_balance,
  rate.read/record), `Stock`, `Production` (departments/permission.read only).
  `SeedTenantRolesAsync` runs at provisioning.
- **Honest gap:** role checks today are `[Authorize(Roles=…)]` on the token — the
  `(resource, action)` catalogue is the *granting* vocabulary; an
  `IPermissionRequirement` handler that enforces keys at the gate is not yet built
  (documented in `PermissionCatalogue`'s own remarks).

`TenantResolutionMiddleware` (between authentication and authorization): authenticated
request → read `tid` claim → `ambientTenant.Set(tenantId)`; token with **no tenant** →
403 `token_carries_no_tenant` — except `/api/v1/auth/me` (self-introspection must be
reachable to diagnose exactly this). Unauthenticated requests pass through (fallback
policy refuses them later).

## Audit trail

`AuditLogEntry` — append-only: `ActorUserId` (null only for refused anonymous attempts,
never a placeholder), `ActorName` snapshot, `Action`, `Resource`, `ResourceId`,
`Outcome` (Allowed/Denied/Failed), `Detail` jsonb, `CorrelationId`, `SourceAddress`,
`OccurredAt`. Tenant-owned + query-filtered. The `MakeAuditTrailAppendOnly` migration
revokes UPDATE/DELETE (legacy's table existed in **no** migration and its writer
swallowed every exception).

`IAuditWriter.Append(AuditRecord)` → `DatabaseAuditWriter` **stages into the caller's
DbContext** — never saves itself. Audit row + business change commit together or not at
all (legacy: failed audit write looked identical to no event). Callers own the unit of
work; appending without a later save loses the row — documented hazard. Failures
propagate: an unauditable change doesn't happen.

The `Poster` writes `ledger.posting.post` / `.retry` / `.reject` records including
refusals — a rejected attempt is evidence, not noise.

## Provisioning flow (how a company comes to exist)

1. `tenants` row created via `ITenantDirectory.CreateAsync` (system scope).
2. `TenantScope.Open(tenantId)` → `PermissionSeeder.SeedTenantRolesAsync` (5 roles+grants).
3. `ILedgerProvisioner.ProvisionAsync(fiscalYear)` → 12 periods + base CoA + 6 journals.
4. `IOpeningBalances.PostAsync` → the founding `OPEN` journal (residual → Owners Capital).
5. `superuser create` → first `Owner` account (offline only).

## Failure types (`Platform/Failures`)

`NotFoundException` (same answer for "absent" and "another tenant's" — no enumeration),
`InvalidCredentialsException`, `AccountLockedException`, `RuleViolationException`.
