# Ostagar Wiki

Feature documentation for the Ostagar codebase, written from the code itself — not from
`docs/progress.md` (stale) or memory. `docs/MASTER_PLAN.md` remains the authoritative
phase/gate record; this wiki explains **how the system works**.

## Reading order

| # | Page | What it covers |
|---|------|----------------|
| 01 | [Product and vision](01-product-and-vision.md) | What Ostagar is, who buys it, the legacy failure it replaces, phase map P0–P9 |
| 02 | [Architecture overview](02-architecture-overview.md) | Solution layout, layering law, composition root, hosts, dependency rules |
| 03 | [Ledger kernel](03-ledger-kernel.md) | Chart of accounts, journals, GL entries/lines, posting engine, invariants I1–I8, reversal, FX, opening balances |
| 04 | [Tenancy, identity, RBAC](04-tenancy-identity-rbac.md) | Tenant model, triple-layer isolation (RLS + check + query filters), JWT, roles/permissions, audit trail |
| 05 | [Persistence and data layer](05-persistence-data-layer.md) | `OstagarDbContext`, EF mapping conventions, migrations, interceptors, provisioning, RLS probe |
| 06 | [HTTP API surface](06-http-api-surface.md) | Middleware pipeline, endpoints, authN/Z, request/response shapes, error contract |
| 07 | [Cash book module](07-cash-book-module.md) | Receipt/payment documents posting through the engine |
| 08 | [Document workflow module](08-document-workflow-module.md) | State machines, transitions, history, document numbering, print readiness |
| 09 | [Partners module](09-partners-module.md) | The partner aggregate: identifiers, credit limits, duplicates, price lists, payment terms |
| 10 | [Web frontend](10-web-frontend.md) | Next.js app, i18n (en/bn), design tokens, API client, auth flow, current screens |
| 11 | [Testing and engineering laws](11-testing-engineering-laws.md) | `verify-all.sh`, the law suite + firepower probes, test projects, CI gates |
| 12 | [Infrastructure and operations](12-infrastructure-operations.md) | Docker topology, images, ports, secrets, CLI commands, CI pipeline |
| 13 | [Design language](13-design-language.md) | `design/DESIGN.md` direction, token system, Bangla typography rules |
| 14 | [Known gaps and review findings](14-known-gaps-and-review-findings.md) | Bugs and gaps found while reviewing, deferred decisions, honest status |

## Ground rules that shape everything

- **The ledger is the kernel.** Modules never compute financial figures from their own
  tables; they emit a `PostingRequest` through `IPoster`. Reports (P4) will be projections
  re-derivable from GL.
- **Invariants live in Postgres.** Balance, immutability, period locks and side-XOR are
  triggers/CHECKs — the C# checks exist to produce readable errors, not to be the rule.
- **Every law must be provably able to fail.** `LawFirepowerTests` plants violations and
  asserts the checkers react.
- **Bangla is first-class.** i18n layer from P0, `bn-x-icu` collation on searchable text,
  Bengali font pairing — see [13](13-design-language.md) and
  [04](04-tenancy-identity-rbac.md).
- **Nothing fakes success.** No `Guid.NewGuid()` as a returned id, no mock data reachable
  in production builds, no commented-out DI registrations.
