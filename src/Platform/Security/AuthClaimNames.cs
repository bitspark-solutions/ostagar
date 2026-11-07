namespace Ostagar.Platform.Security;

/// <summary>
/// The claim names the product reads, in one place.
/// </summary>
/// <remarks>
/// One name per fact, enforced by having no other string to type. The legacy build used
/// <c>ClaimTypes.NameIdentifier</c> in the token, <c>"sub"</c> in the middleware and <c>"id"</c> in a
/// helper, all for the same subject, and the subject resolved to whichever one a given code path
/// happened to read. JSON web tokens are read here with their wire names and nothing is remapped.
/// </remarks>
public static class AuthClaimNames
{
    /// <summary>The only name that identifies the authenticated subject.</summary>
    public const string Subject = "sub";

    /// <summary>The only name that identifies the tenant a token was issued for.</summary>
    public const string Tenant = "tid";

    public const string Email = "email";

    public const string FullName = "name";
}
