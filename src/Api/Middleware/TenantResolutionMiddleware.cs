using Ostagar.Core;
using Ostagar.Platform.Security;
using Ostagar.Platform.Tenancy;

namespace Ostagar.Api.Middleware;

/// <summary>
/// Resolves the tenant carried by the access token and puts it on the request scope.
/// </summary>
/// <remarks>
/// Runs after authentication and before the endpoints, so no handler can query without a tenant
/// already resolved. A token that authenticates a person but names no tenant is refused here with
/// 403 rather than being allowed through to an empty result: an unscoped request reaching the
/// database is a defect, and the session interceptor agrees — it will not open a connection
/// otherwise.
/// </remarks>
public sealed class TenantResolutionMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(
        HttpContext context,
        ICurrentUser currentUser,
        AmbientTenantContext ambientTenant)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var tenantId = currentUser.TenantId;

            if (!tenantId.IsEmpty)
            {
                ambientTenant.Set(tenantId);
            }
            else if (!IsSelfIntrospection(context.Request.Path))
            {
                // A token that authenticates a person but names no tenant is refused before the
                // handler runs, rather than reaching the database and returning an empty list that
                // would look like an empty company.
                //
                // The introspection endpoint is exempt because reporting what the server resolved is
                // its entire purpose — enforcing here would make the one diagnostic unreachable
                // exactly when it is needed.
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(new { error = "token_carries_no_tenant" }, context.RequestAborted);
                return;
            }
        }

        await next(context);
    }

    private static bool IsSelfIntrospection(PathString path) =>
        path.StartsWithSegments("/api/v1/auth/me");
}
