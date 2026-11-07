namespace Ostagar.Platform.Identity;

/// <summary>The result of a successful credential check.</summary>
/// <param name="Token">The signed access token.</param>
/// <param name="ExpiresAtUtc">When the token stops being accepted.</param>
public sealed record IssuedAccessToken(string Token, DateTimeOffset ExpiresAtUtc);
