# P0 Security Scrub — garments-software — Progress Report (2026-10-15)

## What happened
Public repo history rewritten (user-approved, D1) and force-pushed. New HEAD: `fa50730`.

## Verified actions
1. **History rewrite** (git-filter-repo, 3 passes, mirror backup kept at `~/.hermes/cache/scratch/garments-mirror-backup.git`):
   - Removed from ALL 106 commits: `pgadmin/pgpass`, `sample_csv/` (real Demo Garments Ltd financials), `error_docs/`, `PROJECT_PLAN_AUTHENTICATION_FIX.md`, `Enterprise Accounting Software Development Prompt.txt` (root + `documents/` + `docs/` copies).
   - `--replace-text` neutralized baked secrets in every historical blob: `postgres123`, JWT secret `YourSuperSecret…`, AES key/IV/salt, `GarmentsDB@2026!Secure…`.
   - Full byte-level scan of every object in the rewritten repo: **0 leaked bytes, 0 sensitive path refs**.
2. **Forward hygiene commit**: docker-compose now passes `Encryption__Key/IV/Salt` from env; `.env.example` gained `ENCRYPTION_*` entries; `.gitignore` never-tracks `pgpass`/`sample_csv/`.
3. HEAD `appsettings.json` values replaced with `REPLACE_ME_*` placeholders; runtime secrets now come from `.env` via compose.
4. Working copy replaced at `~/Projects/garments-software` (old one kept as `garments-software.pre-scrub-backup`).

## Follow-ups for the owner
- **Local rotation**: set a fresh `POSTGRES_PASSWORD` in local `.env` and recreate the postgres volume (`docker compose down -v`) — the old password was public.
- Anyone who cloned before the force-push must re-clone (hashes changed: `4de1849` → `fa50730`).
- GitHub may briefly serve cached blobs of the old history; treat every old credential as burned (they are now placeholders anyway).

## Next (from master plan)
Phase 2 item 1: re-wire Trial Balance (DI registrations + seeding are commented out in `Program.cs`; sidebar route 404 `/admin/accounting/trial-balance` vs real `/trial-balance`), then real xunit project, README, verify-all.sh.

## Method note
On Windows, git-filter-repo `--blob-callback` crashes git fast-import ("could not write blob … Invalid argument"). Use the native `--replace-text <file>` mode for content scrubbing — one-pass, fast, verified.
