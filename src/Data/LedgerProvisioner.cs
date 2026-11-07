using Ostagar.Core;
using Ostagar.Ledger;
using Ostagar.Platform.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace Ostagar.Data;

/// <summary>
/// Creates a company's periods, base chart of accounts and standard journals.
/// </summary>
/// <remarks>
/// The fiscal year start month is read from the tenant rather than assumed to be July. Every tenant in
/// v1 uses the Bangladeshi July–June year, and the column exists so a later one does not need a schema
/// change — but a provisioner that hardcoded month 7 would give such a company twelve periods pointing
/// at the wrong months and a period-lock schedule that closes the wrong weeks, with every number in the
/// ledger arriving inside a period whose name is a lie.
/// </remarks>
public sealed class LedgerProvisioner(
    OstagarDbContext context,
    ITenantContext tenantContext) : ILedgerProvisioner
{
    public async Task ProvisionAsync(int fiscalYear, CancellationToken cancellationToken = default)
    {
        var tenantId = RequireTenant();

        var tenant = await context.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(each => each.Id == tenantId, cancellationToken)
            .ConfigureAwait(false);

        if (tenant is null)
        {
            throw new InvalidOperationException(
                $"Tenant {tenantId} has no directory row, so its fiscal year cannot be resolved. "
                + "Create the tenant first; provisioning books is the step after tenancy, not part of it.");
        }

        var now = DateTimeOffset.UtcNow;

        await ProvisionPeriodsAsync(fiscalYear, tenantId, tenant, now, cancellationToken).ConfigureAwait(false);
        await ProvisionAccountsAsync(tenantId, now, cancellationToken).ConfigureAwait(false);
        await ProvisionJournalsAsync(tenantId, now, cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> IsProvisionedAsync(int fiscalYear, CancellationToken cancellationToken = default)
    {
        RequireTenant();

        var periodCount = await context.FiscalPeriods
            .CountAsync(each => each.FiscalYear == fiscalYear, cancellationToken)
            .ConfigureAwait(false);

        // Twelve is the whole year, and a partial year is precisely the interrupted run this method
        // exists to detect. Anything between one and eleven is not provisioned.
        return periodCount == 12;
    }

    private async Task ProvisionPeriodsAsync(
        int fiscalYear,
        TenantId tenantId,
        Tenant tenant,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var existing = await context.FiscalPeriods
            .Where(each => each.FiscalYear == fiscalYear)
            .Select(each => each.Number)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var missing = FiscalCalendar
            .YearOf(fiscalYear, tenant.FiscalYearStartMonth)
            .Where(span => !existing.Contains(span.Number))
            .ToList();

        if (missing.Count == 0)
        {
            return;
        }

        foreach (var span in missing)
        {
            context.FiscalPeriods.Add(new FiscalPeriod
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Number = span.Number,
                FiscalYear = span.FiscalYear,
                StartDate = span.StartDate,
                EndDate = span.EndDate,
                Name = span.Name,
                NameBangla = span.NameBangla,
            });
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Writes the base chart, parents first.
    /// </summary>
    /// <remarks>
    /// Ordering is not cosmetic. Each account's ltree path is built from its parent's stored path, so a
    /// leaf processed before its group would either throw on a missing parent or, worse, be written with
    /// a path assembled from a code rather than a resolved ancestor — a subtree that no ancestor query
    /// can find and whose balance therefore appears on no roll-up.
    /// </remarks>
    private async Task ProvisionAccountsAsync(
        TenantId tenantId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var byCode = (await context.Accounts
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false)).ToDictionary(account => account.Code, StringComparer.Ordinal);

        foreach (var spec in BaseChartOfAccounts.Accounts)
        {
            if (byCode.ContainsKey(spec.Code))
            {
                continue;
            }

            var parentId = spec.ParentCode is null ? (Guid?)null : byCode[spec.ParentCode].Id;

            var account = new ChartOfAccount
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Code = spec.Code,
                Name = spec.Name,
                NameBangla = spec.NameBangla,
                Type = spec.Type,
                SubType = spec.SubType,
                ParentId = parentId,
                Path = spec.ParentCode is null
                    ? spec.Code
                    : $"{byCode[spec.ParentCode].Path}.{spec.Code}",
                IsPostable = spec.IsPostable,
                IsActive = true,
                CreatedAt = now,
            };

            context.Accounts.Add(account);

            // Added to the map before the next save, so children resolve against it even though the
            // parent is still only tracked. The path is derived from the model, not from the database.
            byCode[spec.Code] = account;
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task ProvisionJournalsAsync(
        TenantId tenantId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var existingCodes = await context.Journals
            .Select(each => each.Code)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var missing = StandardJournals.Journals
            .Where(spec => !existingCodes.Contains(spec.Code, StringComparer.Ordinal))
            .ToList();

        if (missing.Count == 0)
        {
            return;
        }

        foreach (var spec in missing)
        {
            context.Journals.Add(new Journal
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Code = spec.Code,
                Name = spec.Name,
                NameBangla = spec.NameBangla,
                NumberPrefix = spec.NumberPrefix,
                IsActive = true,
            });
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private TenantId RequireTenant()
    {
        var tenantId = tenantContext.TenantId;

        if (tenantId.IsEmpty)
        {
            throw new InvalidOperationException(
                "Provisioning needs a resolved tenant. Opening a system scope here would write one "
                + "company's chart of accounts against a tenant id nobody chose.");
        }

        return tenantId;
    }
}
