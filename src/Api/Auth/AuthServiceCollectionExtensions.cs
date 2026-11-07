using System.Text;
using Ostagar.Platform.Auth;
using Ostagar.Data;
using Ostagar.Platform.Identity;
using Ostagar.Platform.Rbac;
using Ostagar.Platform.Security;
using Ostagar.Platform.Tenancy;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;

namespace Ostagar.Api.Auth;

/// <summary>
/// Registers identity, token validation and the authorisation default.
/// </summary>
/// <remarks>
/// The fallback policy is <c>RequireAuthenticatedUser</c>. The legacy build called a bare
/// <c>AddAuthorization()</c>, which means "no opinion", which means every action without an
/// attribute was public — including one that minted administrator accounts. Denying by default makes
/// exposure a decision someone had to write down.
/// </remarks>
internal static class AuthServiceCollectionExtensions
{
    public static IServiceCollection AddOstagarAuth(
        this IServiceCollection services,
        AccessTokenOptions tokenOptions)
    {
        services.AddSingleton(tokenOptions);
        services.AddSingleton<AccessTokenIssuer>();

        services.AddIdentityCore<ApplicationUser>()
            .AddRoles<ApplicationRole>()
            .AddEntityFrameworkStores<OstagarDbContext>();

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, CurrentUserService>();
        services.AddScoped<ICurrentActor, CurrentActorService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<PermissionSeeder>();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                // Without this, JwtBearer rewrites the wire names on the way in: `sub` arrives as
                // .../claims/nameidentifier and `tid` as .../claims/tenantid, so every reader keyed
                // on AuthClaimNames silently finds nothing. The result is not a visible error — the
                // caller authenticates, the tenant resolves to empty, and every tenant-scoped request
                // is refused or returns nothing. AuthClaimNames promises that wire names are not
                // remapped; this is what makes that promise true.
                options.MapInboundClaims = false;

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = tokenOptions.Issuer,
                    ValidateAudience = true,
                    ValidAudience = tokenOptions.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(tokenOptions.SigningKey)),
                    ValidateLifetime = true,
                    // The legacy tree set this to false and kept it false in production. Clock skew
                    // tolerance is a convenience for misconfigured clients, not a licence to accept
                    // tokens whose validity window has closed.
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = AuthClaimNames.FullName,
                };
            });

        services.AddAuthorization(options =>
        {
            options.FallbackPolicy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build();
        });

        return services;
    }

    /// <summary>
    /// Reads token settings from the environment, refusing to start without them.
    /// </summary>
    public static AccessTokenOptions ReadAccessTokenOptions(IConfiguration configuration)
    {
        var section = configuration.GetSection(AccessTokenOptions.SectionName);

        var options = new AccessTokenOptions
        {
            Issuer = section[nameof(AccessTokenOptions.Issuer)] ?? string.Empty,
            Audience = section[nameof(AccessTokenOptions.Audience)] ?? string.Empty,
            SigningKey = section[nameof(AccessTokenOptions.SigningKey)] ?? string.Empty,
            LifetimeMinutes = int.TryParse(section[nameof(AccessTokenOptions.LifetimeMinutes)], out var minutes)
                ? minutes
                : 30,
        };

        options.Validate();
        return options;
    }
}
