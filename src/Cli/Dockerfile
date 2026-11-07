# Build context is the repository root. The CLI shares the API's project graph minus the web host —
# same restore layer shape, so a dependency bump changes both images in one pull.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

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
COPY src/Cli/Ostagar.Cli.csproj src/Cli/packages.lock.json src/Cli/

RUN dotnet restore src/Cli/Ostagar.Cli.csproj

COPY src/ src/
RUN dotnet publish src/Cli/Ostagar.Cli.csproj -c Release -o /app/publish /p:UseAppHost=false --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine-extra AS final
# Same musl/Alpine rationale as the API image. musl stays at 1.2.6-r3: the edge r6 build's
# hardened DNS validation rejects Docker's embedded DNS, so the pin is incompatible with
# container networking - CVE-2026-107891 is accepted until 3.24 backports r6. ICU included for
# Bangla-safe string handling.
RUN apk upgrade --no-cache

COPY --from=build --chown=app:app /app/publish .

USER app
ENTRYPOINT ["dotnet", "ostagar-cli.dll"]
