# Build context is the repository root: the API's project graph pulls Core, Ledger, Platform, Data
# and five modules, none of which can restore in isolation.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Central package management files first, then every project file — the restore layer below only
# rebuilds when an actual dependency changes, not on every source edit.
COPY Directory.Build.props Directory.Packages.props .editorconfig ./
COPY src/Core/Ostagar.Core.csproj src/Core/packages.lock.json src/Core/
COPY src/Ledger/Ostagar.Ledger.csproj src/Ledger/packages.lock.json src/Ledger/
COPY src/Platform/Ostagar.Platform.csproj src/Platform/packages.lock.json src/Platform/
COPY src/Data/Ostagar.Data.csproj src/Data/packages.lock.json src/Data/
COPY src/Modules/CashBook/Ostagar.Modules.CashBook.csproj src/Modules/CashBook/packages.lock.json src/Modules/CashBook/
COPY src/Modules/DocumentWorkflow/Ostagar.Modules.DocumentWorkflow.csproj src/Modules/DocumentWorkflow/packages.lock.json src/Modules/DocumentWorkflow/
COPY src/Modules/Inventory/Ostagar.Modules.Inventory.csproj src/Modules/Inventory/packages.lock.json src/Modules/Inventory/
COPY src/Modules/Partners/Ostagar.Modules.Partners.csproj src/Modules/Partners/packages.lock.json src/Modules/Partners/
COPY src/Modules/Payroll/Ostagar.Modules.Payroll.csproj src/Modules/Payroll/packages.lock.json src/Modules/Payroll/
COPY src/Modules/Production/Ostagar.Modules.Production.csproj src/Modules/Production/packages.lock.json src/Modules/Production/
COPY src/Modules/Purchasing/Ostagar.Modules.Purchasing.csproj src/Modules/Purchasing/packages.lock.json src/Modules/Purchasing/
COPY src/Modules/Sales/Ostagar.Modules.Sales.csproj src/Modules/Sales/packages.lock.json src/Modules/Sales/
COPY src/Api/Ostagar.Api.csproj src/Api/packages.lock.json src/Api/

RUN dotnet restore src/Api/Ostagar.Api.csproj

COPY src/ src/
RUN dotnet publish src/Api/Ostagar.Api.csproj -c Release -o /app/publish /p:UseAppHost=false --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine-extra AS final
# musl runtime instead of glibc: the noble glibc CVEs (CVE-2026-89092, CVE-2026-18374) are
# eliminated by construction, and the apk upgrade pulls every fix the 3.24 stream carries
# (zlib 1.4-r0). musl itself stays at the image's 1.2.6-r3 deliberately: the patched r6 build
# (currently only in the edge stream) hardens DNS response validation in a way that rejects
# Docker's embedded DNS proxy, so a container pinned to it cannot resolve the `postgres` service
# name. CVE-2026-107891 is accepted until 3.24 backports r6 - it needs attacker-controlled DNS
# replies, which the container's resolver never sees. Re-scan on bump: docker scout cves ostagar-api:dev
RUN apk upgrade --no-cache

COPY --from=build --chown=app:app /app/publish .

USER app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

# The compose file owns the wiring for database and signing keys; this image refuses to start
# without them, which is the application's own guard rather than a Docker concern.
HEALTHCHECK --interval=15s --timeout=3s --start-period=10s --retries=5 \
    CMD ["wget", "-q", "-O", "-", "http://127.0.0.1:8080/health/live"]

ENTRYPOINT ["dotnet", "Ostagar.Api.dll"]
