#!/usr/bin/env bash
# verify-all.sh — the one command that decides whether a phase is done.
#
# Existence reason: the legacy tree shipped a frontend that could not build and a backend whose
# accounting services were commented out, while docs claimed completion. Nothing checked. Prose
# status is not evidence; this script is. A phase is complete when this prints PASS.
#
# Usage:  ./verify-all.sh            full gate
#         ./verify-all.sh --quick    skip container-backed integration stages
set -uo pipefail
cd "$(dirname "$0")"

QUICK=0
[[ "${1:-}" == "--quick" ]] && QUICK=1

SOLUTION="Ostagar.slnx"
FAILED=()
PASSED=()

step() {
  local name="$1"; shift
  local log; log="$(mktemp)"
  printf '\n\033[1m▶ %s\033[0m\n' "$name"
  if "$@" > "$log" 2>&1; then
    PASSED+=("$name")
    printf '  \033[32mPASS\033[0m %s\n' "$name"
  else
    FAILED+=("$name")
    printf '  \033[31mFAIL\033[0m %s\n' "$name"
    tail -n 40 "$log" | sed 's/^/      /'
  fi
  rm -f "$log"
}

# A test step that runs nothing is not a pass. `dotnet test --no-build` exits 0 with an empty log when
# the project was never compiled — which is what happens to any test project left out of the solution:
# the build stage skips it, `--no-build` skips it again, and the gate prints PASS over zero tests. This
# reads the summary and fails unless tests actually ran.
step_tests() {
  local name="$1"; shift
  local log; log="$(mktemp)"
  printf '\n\033[1m▶ %s\033[0m\n' "$name"
  if "$@" > "$log" 2>&1; then
    local total
    total=$(
      {
        grep -oE 'Total: +[0-9]+' "$log" | awk '{ s += $2 } END { print s + 0 }'
        grep -oE 'Tests: +[0-9]+ passed' "$log" | awk '{ s += $2 } END { print s + 0 }'
      } | awk '{ s += $1 } END { print s + 0 }'
    )
    if [ "${total:-0}" -gt 0 ]; then
      PASSED+=("$name")
      printf '  \033[32mPASS\033[0m %s \033[2m(%s tests)\033[0m\n' "$name" "$total"
    else
      FAILED+=("$name (ran 0 tests)")
      printf '  \033[31mFAIL\033[0m %s reported success without running a single test.\n' "$name"
      printf '      The suite was never compiled — check it is listed in %s.\n' "$SOLUTION"
      tail -n 20 "$log" | sed 's/^/      /'
    fi
  else
    FAILED+=("$name")
    printf '  \033[31mFAIL\033[0m %s\n' "$name"
    tail -n 40 "$log" | sed 's/^/      /'
  fi
  rm -f "$log"
}

# ---------------------------------------------------------------- backend
step "backend: restore" dotnet restore "$SOLUTION"
step "backend: build (warnings are errors)" dotnet build "$SOLUTION" --no-restore -v q --nologo

# Engineering law L1/L1b/L2/L3/L6/L8. These fail the build, not a reviewer.
step_tests "architecture: engineering laws" dotnet test tests/Ostagar.Tests.Architecture --no-build -v q --nologo
step_tests "unit: domain primitives" dotnet test tests/Ostagar.Tests.Unit --no-build -v q --nologo


# The vacuity guard above catches a suite that ran nothing, but not why. The cause is a test project
# sitting on disk while absent from the solution: the build stage compiles 19 projects and never sees
# the twentieth, so its assembly is missing and `--no-build` reports success over zero tests. Partners
# passed exactly that way until it was listed — so set membership is a check, not a convention.
step "tests: every suite is compiled by the solution" bash -c '
  set -e
  orphans=""
  count=0
  for project in $(find tests -mindepth 2 -maxdepth 2 -name "*.csproj"); do
    count=$((count + 1))
    grep -q "Path=\"$project\"" Ostagar.slnx || orphans="$orphans  $project"$'\n'
  done
  if [ -n "$orphans" ]; then
    echo "These test projects exist but are NOT listed in Ostagar.slnx, so the build never compiles"
    echo "them and dotnet test --no-build would pass without running a single test:"
    printf "%s" "$orphans"
    exit 1
  fi
  echo "$count test projects, all listed in the solution"
'

# Module suites are DISCOVERED, not listed by name. Naming each project here meant a new test project
# could exist, build, pass review, and never be run by the gate — which is the exact shape of
# silence that made the legacy project's checks meaningless: "all accounting services registered"
# was asserted against a list nobody had populated. An unlisted project is worse than a failing one,
# because a failing one gets fixed.
MODULE_SUITES=()
while IFS= read -r suite_dir; do
  MODULE_SUITES+=("$suite_dir")
done < <(find tests -mindepth 2 -maxdepth 2 -name '*.csproj' \
    ! -name 'Ostagar.Tests.Architecture.csproj' \
    ! -name 'Ostagar.Tests.Unit.csproj' \
    ! -name 'Ostagar.Tests.Integration.csproj' \
    -printf '%h\n' 2>/dev/null | sort -u)

if [ "${#MODULE_SUITES[@]}" -gt 0 ]; then
  for suite in "${MODULE_SUITES[@]}"; do
    step_tests "module tests: $(basename "$suite")" dotnet test "$suite" --no-build -v q --nologo
  done
else
  printf '\n\033[1m▶ module tests\033[0m\n'
  printf '  (none discovered — modules currently ship no test project of their own)\n'
fi
# The integration host boots the real Program, which seeds the permission catalogue and therefore
# needs a live database. It deliberately has no fallback connection string, so the gate provides one.
step_tests "integration: tenant isolation and ledger invariants on a real database" bash -c '
  set -e
  POSTGRES_PASSWORD=$(grep -E "^POSTGRES_PASSWORD=" .env | cut -d= -f2-)
  export ConnectionStrings__Default="Host=127.0.0.1;Port=6432;Database=ostagar;Username=postgres;Password=$POSTGRES_PASSWORD"

  # Bring the developer database up to the current model before testing against it. P2 found the
  # ledger invariant suite failing on a stale schema with an error about Npgsql not knowing the
  # `ltree` type — which reads as a driver bug and is really a migration that was never applied
  # here, because the gate only applied migrations to a throwaway container it then threw away.
  dotnet ef database update --context OstagarDbContext \
    --project src/Data --startup-project src/Api --no-build

  dotnet test tests/Ostagar.Tests.Integration --no-build -v q --nologo
'

# ---------------------------------------------------------------- boot
# Building is not starting. The legacy tree proved nothing about boot: its accounting controllers
# threw at activation because 22 registrations sat commented out, and the one thing that would have
# caught it cheaply — "does the process come up and answer" — was never run.
#
# The app refuses to start without a configured database, so the gate provides one instead of
# weakening the app. Credentials stay in the untracked .env and reach the process as environment
# variables, which is the only path .NET config uses for them; no dev password is committed.
if [[ "$QUICK" -eq 0 ]] || [[ -f .env ]]; then
  docker compose --env-file .env -f infra/compose.dev.yml up -d >/dev/null 2>&1 || true
fi

# Kestrel is told to bind port 0 and the gate reads back whatever it actually got. This machine
# reserves whole 100-port blocks to Hyper-V whenever Docker Desktop restarts (currently 7122-7221,
# previously 5432-5435 and 5199), so any hardcoded dev port eventually fails for a reason that has
# nothing to do with the code — and a gate that fails for environmental reasons gets ignored.
step "app boots and answers /health" bash -c '
  set -e
  LOG=/tmp/ostagar-boot.log
  : > "$LOG"

  POSTGRES_PASSWORD=$(grep -E "^POSTGRES_PASSWORD=" .env | cut -d= -f2-)
  export ConnectionStrings__Default="Host=127.0.0.1;Port=6432;Database=ostagar;Username=postgres;Password=$POSTGRES_PASSWORD"
  export ASPNETCORE_ENVIRONMENT=Development

  # The application refuses to start without a signing key rather than generating one, so the gate
  # has to supply the dev key from the untracked .env. If it is missing, this fails loudly — which is
  # the intended behaviour, not a gate bug.
  while IFS= read -r line; do
    case "$line" in OstagarAuth__*) export "${line%%=*}=${line#*=}";; esac
  done < .env

  dotnet run --project src/Api --no-build --no-launch-profile \
    --urls "http://127.0.0.1:0" > "$LOG" 2>&1 &
  PID=$!

  cleanup() {
    # dotnet run spawns the real host as a child; killing only the parent leaks the child and
    # squats a port for every later run.
    kill "$PID" 2>/dev/null || true
    pkill -f "Ostagar.Api" 2>/dev/null || true
    wait "$PID" 2>/dev/null || true
  }
  trap cleanup EXIT

  PORT=""
  for _ in $(seq 1 40); do
    PORT=$(grep -oE "Now listening on: http://[0-9.]+:[0-9]+" "$LOG" | grep -oE "[0-9]+$" | head -1)
    [ -n "$PORT" ] && break
    kill -0 "$PID" 2>/dev/null || break
    sleep 1
  done

  if [ -z "$PORT" ]; then
    echo "never reported a bound address:"; tail -n 25 "$LOG"; exit 1
  fi

  if ! curl -fsS "http://127.0.0.1:$PORT/health/live" >/dev/null 2>&1; then
    echo "bound port $PORT but /health/live did not answer:"; tail -n 25 "$LOG"; exit 1
  fi
  curl -fsS "http://127.0.0.1:$PORT/health/ready" > /dev/null
  echo "healthy on ephemeral port $PORT"
'

# ---------------------------------------------------------------- frontend
if [[ -d web ]]; then
  step "frontend: install" bash -c "cd web && npm ci --no-audit --no-fund || npm install --no-audit --no-fund"
  # Real exit code, no pipe: `npm run build | tail` reports tail's status and hides failures.
  step "frontend: type-check" bash -c "cd web && npm run type-check"
  step "frontend: lint (L12-L15, L17)" bash -c "cd web && npm run lint"
  step "frontend: production build" bash -c "cd web && npm run build"
  step "frontend: route graph closed (L14)" bash -c "cd web && node scripts/check-route-graph.mjs"
  # 105 jest tests existed while the gate never ran them, so a broken message layer or API client
  # could ship green. Type-checking proves the code compiles; only these prove it behaves.
  step_tests "frontend: behaviour tests (jest)" bash -c "cd web && npm test --silent"
else
  echo "  (web/ not present yet — frontend gate skipped; required from P1 onward)"
fi

# ---------------------------------------------------------------- database
if [[ "$QUICK" -eq 0 ]]; then
  if ! docker info >/dev/null 2>&1; then
    FAILED+=("database: migrations on empty Postgres (Docker daemon not running)")
    printf '\n\033[31mFAIL\033[0m Docker daemon is down; the empty-database migration gate cannot run.\n'
  else
    # L7: migrations must apply to an EMPTY database. Half-migrated local volumes hid the legacy
    # tree's SQL-Server-typed migration for a year because everyone's database was already dirty.
    if find src -path '*/Migrations/*Designer.cs' -print -quit | grep -q .; then
      step "database: migrations apply to empty Postgres" bash -c '
        set -e
        C=garments-verify-pg-$$
        docker run -d --rm --name "$C" -e POSTGRES_PASSWORD=verify -e POSTGRES_DB=garments -p 127.0.0.1:0:5432 postgres:17-alpine >/dev/null
        trap "docker stop $C >/dev/null 2>&1 || true" EXIT
        for i in $(seq 1 60); do docker exec "$C" pg_isready -U postgres >/dev/null 2>&1 && break; sleep 1; done

        # The published host port is chosen by Docker, so it must be read back. Connecting to a bare
        # 5432 instead reaches whatever else is listening there and fails on the password of a
        # different server entirely.
        PORT=$(docker port "$C" 5432 | head -1 | sed 's/.*://')

        # The design-time factory reads this variable, so it must point at the throwaway container;
        # passing --connection alone would be ignored and the gate would migrate the developer database.
        export ConnectionStrings__Default="Host=127.0.0.1;Port=$PORT;Database=ostagar;Username=postgres;Password=verify"

        # The project that holds the migrations is named here, so it is a fact the gate has to keep
        # true. P2 moved them from src/Platform to src/Data; a stale path reads as "No DbContext named
        # 'OstagarDbContext' was found", which looks like a broken model and is really the gate
        # pointing at the wrong assembly.
        dotnet ef database update --context OstagarDbContext \
          --project src/Data --startup-project src/Api --no-build

        # A schema that applies but protects nothing is the failure this whole phase exists to catch.
        # Fed over stdin: writing a Windows path for docker cp is how this went wrong before.
        docker exec -i "$C" psql -U postgres -d ostagar -v ON_ERROR_STOP=1 -q -f - \
          < infra/sql/assert-row-level-security.sql
      '
    else
      printf '\n  (no EF migrations exist yet — L7 database gate activates with P2)\n'
    fi
  fi
fi

# ---------------------------------------------------------------- coverage
# A green check over an empty set is not coverage. The legacy project asserted "all accounting
# services registered" against a list nobody populated, so print the real counts every run.
printf '\n\033[1m▶ gate coverage (informational)\033[0m\n'
printf '  controllers          : %s\n' "$(grep -rlE 'class \w+Controller\b' src --include='*.cs' 2>/dev/null | wc -l | tr -d ' ')"
printf '  endpoints ([Http…])   : %s\n' "$(grep -rhoE '\[Http(Get|Post|Put|Delete|Patch)' src --include='*.cs' 2>/dev/null | wc -l | tr -d ' ')"
printf '  EF migrations        : %s\n' "$(find src -path '*/Migrations/*Designer.cs' 2>/dev/null | wc -l | tr -d ' ')"
printf '  DB check constraints : %s\n' "$(grep -rhoE "name: \"(CK|UQ|FK)_[a-z_]+\"" src --include='*.cs' 2>/dev/null | wc -l | tr -d ' ')"
printf '  source .cs files     : %s\n' "$(find src -name '*.cs' -not -path '*/obj/*' -not -path '*/bin/*' 2>/dev/null | wc -l | tr -d ' ')"
printf '  test .cs files       : %s\n' "$(find tests -name '*.cs' -not -path '*/obj/*' -not -path '*/bin/*' 2>/dev/null | wc -l | tr -d ' ')"

# ---------------------------------------------------------------- verdict
printf '\n════════════════════════════════════════\n'
if [[ ${#FAILED[@]} -eq 0 ]]; then
  printf '\033[32mPASS\033[0m — %d checks green. This phase may be called complete.\n' "${#PASSED[@]}"
  exit 0
fi

printf '\033[31mFAIL\033[0m — %d passed, %d failed:\n' "${#PASSED[@]}" "${#FAILED[@]}"
for failure in "${FAILED[@]}"; do printf '  • %s\n' "$failure"; done
printf '\nNo phase may be reported complete while this prints FAIL.\n'
exit 1
