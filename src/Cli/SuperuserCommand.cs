using Ostagar.Core;
using Ostagar.Data;
using Ostagar.Platform.Rbac;
using Ostagar.Platform.Tenancy;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Ostagar.Cli;

/// <summary>
/// Creates the first account for a company, offline.
/// </summary>
/// <remarks>
/// This exists so there is no HTTP route that mints administrators. The legacy build exposed
/// <c>POST /api/AdminSetup/create-admin</c> with no <c>[Authorize]</c> attribute at all, so anyone who
/// could reach the port could create an administrator — the only reason it was not exploited is that
/// nothing else in the deployment worked.
/// </remarks>
internal static class SuperuserCommand
{
    internal static async Task<int> RunAsync(string[] args)
    {
        var options = CommandOptions.Parse(args);

        if (options.Problems.Count > 0)
        {
            foreach (var problem in options.Problems)
            {
                Console.Error.WriteLine(problem);
            }

            return Usage.ExitCode;
        }

        using var host = CliHost.Build(args).Build();

        var scopeFactory = host.Services.GetRequiredService<IServiceScopeFactory>();

        var tenantId = await ResolveTenantAsync(scopeFactory, options.TenantSlug!).ConfigureAwait(false);

        if (tenantId is null)
        {
            Console.Error.WriteLine($"No tenant with slug '{options.TenantSlug}'.");
            return 2;
        }

        return await CreateAsync(scopeFactory, tenantId.Value, options.Email!, options.Password!)
            .ConfigureAwait(false);
    }

    private static async Task<TenantId?> ResolveTenantAsync(IServiceScopeFactory scopeFactory, string slug)
    {
        using var scope = TenantScope.OpenSystem(scopeFactory);
        var directory = scope.ServiceProvider.GetRequiredService<ITenantDirectory>();
        var tenant = await directory.FindBySlugAsync(slug).ConfigureAwait(false);

        return tenant?.Id;
    }

    private static async Task<int> CreateAsync(
        IServiceScopeFactory scopeFactory,
        TenantId tenantId,
        string email,
        string password)
    {
        using var scope = TenantScope.Open(scopeFactory, tenantId);

        await scope.ServiceProvider.GetRequiredService<PermissionSeeder>()
            .SeedTenantRolesAsync(tenantId)
            .ConfigureAwait(false);

        var users = scope.ServiceProvider.GetRequiredService<UserManager<Platform.Identity.ApplicationUser>>();
        var normalised = email.Trim();

        var existing = await users.FindByEmailAsync(normalised).ConfigureAwait(false);

        if (existing is not null)
        {
            // Idempotent, because re-running provisioning after a partial failure has to be normal
            // rather than something that produces a second owner or an error that gets ignored.
            await users.AddToRoleAsync(existing, DefaultRoleMatrix.Owner).ConfigureAwait(false);
            Console.WriteLine($"Account already existed; {normalised} is now an Owner.");
            return 0;
        }

        var user = new Platform.Identity.ApplicationUser
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            UserName = normalised,
            Email = normalised,
            FullName = "Owner",
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        var result = await users.CreateAsync(user, password).ConfigureAwait(false);

        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                Console.Error.WriteLine($"{error.Code}: {error.Description}");
            }

            return 3;
        }

        await users.AddToRoleAsync(user, DefaultRoleMatrix.Owner).ConfigureAwait(false);

        Console.WriteLine($"Created owner {normalised} for tenant {tenantId}.");
        return 0;
    }
}
