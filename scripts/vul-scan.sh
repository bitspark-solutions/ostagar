#!/bin/bash
# Full vulnerability sweep: package manifests + every runtime image in the dev stack.
# Usage: scripts/vul-scan.sh   (run from the repo root; Docker Desktop must be up)
# The results feed docs/SECURITY_VULN_REGISTER.md — update it when counts change.
set -uo pipefail

say() { printf '\n=== %s ===\n' "$1"; }

say "npm (web/)"
(cd web && npm audit || true)

say "NuGet (solution, incl. transitive)"
dotnet list GarmentsERP.slnx package --vulnerable --include-transitive | grep -E "no vulnerable|vulnerable" | sort -u || true

IMAGES=(garments-api:dev garments-cli:dev garments-web:dev garments-postgres:dev garments-redis:dev)

say "Docker Scout (note: garments-postgres has a documented base-SBOM ghost — see the register)"
for img in "${IMAGES[@]}"; do
  echo "--- $img"
  docker scout cves "$img" 2>&1 | grep -E "vulnerabilities found|No vulnerable packages" || echo "(scout unavailable)"
done

say "Trivy second opinion (CRITICAL/HIGH/MEDIUM — reads the real filesystem)"
for img in "${IMAGES[@]}"; do
  echo "--- $img"
  docker run --rm -v /var/run/docker.sock:/var/run/docker.sock aquasec/trivy:latest \
    image --scanners vuln --severity CRITICAL,HIGH,MEDIUM "$img" 2>&1 \
    | grep -E "Total: [0-9]+ \(|Vulnerability" | head -5 || true
done

say "done — compare against docs/SECURITY_VULN_REGISTER.md"
