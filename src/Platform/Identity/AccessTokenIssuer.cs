using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Ostagar.Core;
using Ostagar.Platform.Security;
using Microsoft.IdentityModel.Tokens;

namespace Ostagar.Platform.Identity;

/// <summary>
/// Mints the access token the whole tenancy model hangs on.
/// </summary>
/// <remarks>
/// The token carries <c>tid</c> as well as <c>sub</c>. That tenant claim is what the request pipeline
/// resolves into the ambient tenant before any database connection opens, so a token without it can
/// authenticate a person and still be refused every row — which is the correct outcome for a
/// credential that cannot say whose books it is asking to read.
/// </remarks>
public sealed class AccessTokenIssuer(AccessTokenOptions options)
{
    public IssuedAccessToken Issue(ApplicationUser user, TenantId tenantId, IEnumerable<string> roles)
    {
        ArgumentNullException.ThrowIfNull(user);

        if (tenantId.IsEmpty)
        {
            throw new ArgumentException(
                "An access token must name the tenant it grants access to.", nameof(tenantId));
        }

        var now = DateTimeOffset.UtcNow;
        var expires = now.AddMinutes(options.LifetimeMinutes);

        var claims = new List<Claim>
        {
            new(AuthClaimNames.Subject, user.Id.ToString("D")),
            new(AuthClaimNames.Tenant, tenantId.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("D")),
        };

        AddIfPresent(claims, AuthClaimNames.Email, user.NormalizedEmail is null ? user.Email : user.Email);
        AddIfPresent(claims, AuthClaimNames.FullName, user.FullName);

        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.SigningKey)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: options.Issuer,
            audience: options.Audience,
            claims: claims,
            notBefore: now.UtcDateTime,
            expires: expires.UtcDateTime,
            signingCredentials: credentials);

        return new IssuedAccessToken(new JwtSecurityTokenHandler().WriteToken(token), expires);
    }

    private static void AddIfPresent(List<Claim> claims, string type, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            claims.Add(new Claim(type, value));
        }
    }
}
