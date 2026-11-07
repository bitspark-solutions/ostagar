using Ostagar.Core;
using Ostagar.Platform.Identity;
using Ostagar.Platform.Tenancy;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Ostagar.Platform.Auth;

/// <summary>
/// Sign-in across the tenant boundary.
/// </summary>
/// <remarks>
/// The order matters and is the reason this is one class rather than a controller action. The tenant
/// is resolved first, through the directory context, because that lookup is the one query that cannot
/// be tenant-scoped — the tenant is what it produces. Only after the tenant is known is a tenant
/// scope opened and <see cref="UserManager{TUser}"/> resolved inside it, so the user lookup already
/// runs under row-level security for that company.
///
/// Doing it the other way round — authenticate, then work out which tenant you are in — is how one
/// company's password ends up being valid against another company's account list.
/// </remarks>
public sealed class AuthService(
    IServiceScopeFactory scopeFactory,
    AccessTokenIssuer tokenIssuer) : IAuthService
{
    public async Task<AuthOutcome> SignInAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.TenantSlug)
            || string.IsNullOrWhiteSpace(request.Email)
            || string.IsNullOrWhiteSpace(request.Password))
        {
            return AuthOutcome.Rejected(AuthFailureReason.IncompleteRequest);
        }

        var tenant = await ResolveTenantAsync(request.TenantSlug, cancellationToken).ConfigureAwait(false);

        if (tenant is null || !tenant.IsActive)
        {
            // Reported identically whether the slug is unknown or the company is closed: neither
            // answer should confirm that a given factory is a customer here.
            return AuthOutcome.Rejected(AuthFailureReason.TenantUnavailable);
        }

        return await SignInWithinTenantAsync(tenant, request, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Tenant?> ResolveTenantAsync(string slug, CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var directory = scope.ServiceProvider.GetRequiredService<ITenantDirectory>();

        return await directory.FindBySlugAsync(slug, cancellationToken).ConfigureAwait(false);
    }

    private async Task<AuthOutcome> SignInWithinTenantAsync(
        Tenant tenant,
        LoginRequest request,
        CancellationToken cancellationToken)
    {
        using var scope = TenantScope.Open(scopeFactory, tenant.Id);
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var email = request.Email.Trim();
        var user = await users.FindByEmailAsync(email).ConfigureAwait(false);

        // One reason for three different failures, so the form cannot be used to enumerate accounts.
        if (user is null || !await users.CheckPasswordAsync(user, request.Password).ConfigureAwait(false))
        {
            return AuthOutcome.Rejected(AuthFailureReason.InvalidCredentials);
        }

        if (!user.IsActive)
        {
            return AuthOutcome.Rejected(AuthFailureReason.AccountDisabled);
        }

        var roles = await users.GetRolesAsync(user).ConfigureAwait(false);
        var token = tokenIssuer.Issue(user, tenant.Id, roles);

        return new AuthOutcome(true, AuthFailureReason.InvalidCredentials, token, [.. roles]);
    }
}
