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

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# The aspnet base image already provides the non-root 'app' user. curl is for the healthcheck;
# libgssapi is for Npgsql's GSSAPI probe, which logs a load error without it on every connection.
RUN apt-get update && apt-get install -y --no-install-recommends curl libgssapi-krb5-2 \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build --chown=app:app /app/publish .

USER app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

# The compose file owns the wiring for database and signing keys; this image refuses to start
# without them, which is the application's own guard rather than a Docker concern.
HEALTHCHECK --interval=15s --timeout=3s --start-period=10s --retries=5 \
    CMD curl -fsS http://localhost:8080/health/live || exit 1

ENTRYPOINT ["dotnet", "Garments.Api.dll"]
