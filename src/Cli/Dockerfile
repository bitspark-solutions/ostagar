# Build context is the repository root. The CLI shares the API's project graph minus the web host —
# same restore layer shape, so a dependency bump changes both images in one pull.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

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
COPY src/Cli/Garments.Cli.csproj src/Cli/packages.lock.json src/Cli/

RUN dotnet restore src/Cli/Garments.Cli.csproj

COPY src/ src/
RUN dotnet publish src/Cli/Garments.Cli.csproj -c Release -o /app/publish /p:UseAppHost=false --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine-extra AS final
# Same musl/Alpine rationale as the API image, including the edge-pinned musl security fix
# (CVE-2026-107891, 1.2.6-r6) - remove the pin once the 3.24 stream carries it. ICU included for
# Bangla-safe string handling.
RUN apk upgrade --no-cache \
    && apk add --no-cache --repository https://dl-cdn.alpinelinux.org/alpine/edge/main musl=1.2.6-r6

COPY --from=build --chown=app:app /app/publish .

USER app
ENTRYPOINT ["dotnet", "garments-cli.dll"]
