# Build context is the repository root: the API's project graph pulls Core, Ledger, Platform, Data
# and five modules, none of which can restore in isolation.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Central package management files first, then every project file — the restore layer below only
# rebuilds when an actual dependency changes, not on every source edit.
COPY Directory.Build.props Directory.Packages.props .editorconfig ./
COPY src/Core/Garments.Core.csproj src/Core/packages.lock.json src/Core/
COPY src/Ledger/Garments.Ledger.csproj src/Ledger/packages.lock.json src/Ledger/
COPY src/Platform/Garments.Platform.csproj src/Platform/packages.lock.json src/Platform/
COPY src/Data/Garments.Data.csproj src/Data/packages.lock.json src/Data/
COPY src/Modules/CashBook/Garments.Modules.CashBook.csproj src/Modules/CashBook/packages.lock.json src/Modules/CashBook/
COPY src/Modules/DocumentWorkflow/Garments.Modules.DocumentWorkflow.csproj src/Modules/DocumentWorkflow/packages.lock.json src/Modules/DocumentWorkflow/
COPY src/Modules/Inventory/Garments.Modules.Inventory.csproj src/Modules/Inventory/packages.lock.json src/Modules/Inventory/
COPY src/Modules/Partners/Garments.Modules.Partners.csproj src/Modules/Partners/packages.lock.json src/Modules/Partners/
COPY src/Modules/Payroll/Garments.Modules.Payroll.csproj src/Modules/Payroll/packages.lock.json src/Modules/Payroll/
COPY src/Modules/Production/Garments.Modules.Production.csproj src/Modules/Production/packages.lock.json src/Modules/Production/
COPY src/Modules/Purchasing/Garments.Modules.Purchasing.csproj src/Modules/Purchasing/packages.lock.json src/Modules/Purchasing/
COPY src/Modules/Sales/Garments.Modules.Sales.csproj src/Modules/Sales/packages.lock.json src/Modules/Sales/
COPY src/Api/Garments.Api.csproj src/Api/packages.lock.json src/Api/

RUN dotnet restore src/Api/Garments.Api.csproj

COPY src/ src/
RUN dotnet publish src/Api/Garments.Api.csproj -c Release -o /app/publish /p:UseAppHost=false --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine-extra AS final
# musl runtime instead of glibc: the noble glibc CVEs (CVE-2026-89092, CVE-2026-18374) are
# eliminated by construction, and the apk upgrade pulls every fix the 3.24 stream carries (zlib
# 1.4-r0). One CVE remained at scan time: musl CVE-2026-107891, fixed in 1.2.6-r6 - the patched
# package ships in the edge stream but is not yet backported to 3.24, so it is pinned from edge
# here. musl's ABI is stable within 1.2.6.x and the runtime is proven to load (dotnet
# --list-runtimes). REMOVE the edge pin once 3.24 ships musl >= 1.2.6-r6, then plain apk upgrade
# covers it. Re-scan on bump: docker scout cves garments-api:dev
RUN apk upgrade --no-cache \
    && apk add --no-cache --repository https://dl-cdn.alpinelinux.org/alpine/edge/main musl=1.2.6-r6

COPY --from=build --chown=app:app /app/publish .

USER app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

# The compose file owns the wiring for database and signing keys; this image refuses to start
# without them, which is the application's own guard rather than a Docker concern.
HEALTHCHECK --interval=15s --timeout=3s --start-period=10s --retries=5 \
    CMD ["wget", "-q", "-O", "-", "http://127.0.0.1:8080/health/live"]

ENTRYPOINT ["dotnet", "Garments.Api.dll"]
