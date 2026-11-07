using System.Security.Claims;
using Ostagar.Core;
using Microsoft.AspNetCore.Http;

namespace Ostagar.Platform.Security;

/// <summary>
/// Reads the caller out of the access token on the current request.
/// </summary>
/// <remarks>
/// Every value comes from <see cref="AuthClaimNames"/> and nowhere else. The legacy build had three
/// different claim names for the same subject across one subsystem, so which identity you got
/// depended on which file asked — and the audit trail recorded a hard-coded guid behind a
/// <c>// TODO</c> instead of a person.
/// </remarks>
public sealed class CurrentUserService(IHttpContextAccessor accessor) : ICurrentUser
{
    private ClaimsPrincipal? Principal => accessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;

    public TenantId TenantId =>
        TenantId.TryParse(Principal?.FindFirstValue(AuthClaimNames.Tenant), out var tenantId)
            ? tenantId
            : TenantId.None;

    public Guid? UserId =>
        Guid.TryParse(Principal?.FindFirstValue(AuthClaimNames.Subject), out var subject)
            ? subject
            : null;

    public string UserName =>
        Principal?.FindFirstValue(AuthClaimNames.Email)
        ?? Principal?.Identity?.Name
        ?? string.Empty;

    public string DisplayName => Principal?.FindFirstValue(AuthClaimNames.FullName) ?? UserName;
}
