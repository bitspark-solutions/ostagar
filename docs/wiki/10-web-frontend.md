# 10 — Web frontend (`web/`)

Next.js 16 (App Router) + React 19 + MUI 7 + Emotion, TypeScript strict. Very early
stage: auth + shell + one placeholder dashboard. The infrastructure (i18n, tokens,
API client, tests) is built to P0–P4 law, ahead of the screens.

## Structure

```
web/
  src/
    api/         ApiClient (axios), AccessTokenStore, ApiBaseUrl, ApiError family,
                 AuthLoginPath, PresentApiError/ToApiError/ApiErrorPresentation
    app/         layout.tsx (fonts+locale+theme), page.tsx → redirect /login,
                 login/page.tsx, admin/layout.tsx (ApplicationShell),
                 admin/dashboard/page.tsx (EmptyState), error.tsx, globals.css
    auth/        LoginCredentials, AccessTokenPayload, ReadAccessToken, SignIn
    components/  ApplicationShell, AdminNavigationMenu, LoginPanel, PageHeading,
                 EmptyState, ErrorState, LocaleSwitcher, ApplicationDate
    format/      (number/date formatting helpers)
    i18n/        Locale machinery + messages/{en,bn}.ts  (~28 keys each — early)
    navigation/  AdminNavigation (currently one item: admin.dashboard)
    theme/       Tokens.ts (design tokens — single source, L15), AppTheme,
                 ThemeProvider, AppFonts (latin + bengali next/font vars)
  __tests__/     25 jest test files
  scripts/       check-route-graph.mjs (L14: every nav href resolves)
```

## API client (law L12)

- **One** axios instance (`apiClient`) + **one** auth interceptor — the only place
  `localStorage`/`Authorization` may appear (ESLint `no-restricted-imports` enforces).
- `apiBaseUrl()` reads `NEXT_PUBLIC_API_BASE_URL` **per request** — unset → readable
  `ApiError('configuration')`, not a build crash; must be absolute http(s).
- Request interceptor attaches `Bearer <token>` from `accessTokenStore`
  (`localStorage["ostagar.accessToken"]`).
- Response interceptor normalizes everything through `toApiError` →
  `ApiError`/`ApiFailureKind`/`ApiErrorPresentation` — typed failure surface for UI.
- 15s timeout. No raw `fetch`, no second client.

## Auth flow

`signIn({tenantSlug, email, password})` → `POST {authLoginPath}` → reads
`accessToken ?? token` from payload → writes store → boolean. Token absence = signed
out (no assumed session). `LoginPanel` handles the form; `ApplicationShell` wraps
`/admin/*`.

## i18n (law L17 — built from P0)

- `Locale` = `'en' | 'bn'`; `defaultLocale()` (env `NEXT_PUBLIC_DEFAULT_LOCALE`,
  compose passes `bn`); cookie `locale` persists choice; `LocaleProvider` +
  `useTranslations()`/`translateMessage()` with `{placeholder}` interpolation.
- Message catalogs `messages/en.ts` + `messages/bn.ts` — every user-facing string
  goes through them; ESLint fails hardcoded JSX strings.
- `LocaleSwitcher` component; `<html lang>` set from resolved locale; fonts loaded
  as CSS vars (`--font-latin`, `--font-bengali`) so mixed `স্টক / Stock` labels keep
  baseline alignment.

## Theme / design tokens (law L15)

`theme/Tokens.ts` is the **single source**: colors (neutral placeholder palette —
text/surface/border/action/state ramps incl. danger/success/warning/info soft
variants), `radius` (0/4/6 — flat, SAP-influenced), `space` (**explicit px strings** —
a bare number in `sx` would be theme-spacing multiples; the comment records the
128px-gap bug that taught this), `type` (font vars + money/label/body/title sizes),
`density` (table row padding, sticky header height). Components reference tokens
only — no inline hex/rgba (CI grep enforces). `design/DESIGN.md` is the intended
final palette; §9 lists the token edits to apply.

## Pages today

- `/` → redirect `/login`
- `/login` — `LoginPanel` + `LocaleSwitcher`
- `/admin/dashboard` — `ApplicationShell` + `PageHeading` + `EmptyState`
  ("ledger pending" — honest empty state, nothing simulated; L13)
- `/admin/layout.tsx` — shell with `AdminNavigationMenu` (dashboard only)

Route graph check (`npm run route-graph`) proves nav hrefs resolve — currently
3 pages, all components reachable.

## Quality gates (`package.json`)

`npm run type-check` (tsc --noEmit) · `lint` (eslint, flat config) · `test` (jest
--ci, jsdom + testing-library, 25 files covering i18n/api/auth/components) ·
`build` (next build, standalone output) · `route-graph`.

Docker: multi-stage `node:26-alpine3.23`, `npm ci` in deps stage, `next build` with
`NEXT_PUBLIC_*` build-args (inlined at build time — can't change at container start),
standalone runner with npm/corepack stripped (zeroed the language-package CVE count).

## What's missing (by design, pending phases)

No journal/cash-book/partner/inventory screens yet — the API exists but UI arrives
with P4 (reporting/design system) and module phases. No auth guard on `/admin/*`
yet visible in the layout (dashboard is a placeholder); token refresh, route
protection, and the full nav arrive with the screens that need them.
