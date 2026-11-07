namespace Ostagar.Platform.Auth;

/// <summary>Credentials as submitted to the sign-in endpoint.</summary>
/// <param name="TenantSlug">Which company is being signed into — the same email may exist in several.</param>
/// <param name="Email">The account's email address.</param>
/// <param name="Password">The plaintext password, verified against the stored hash and never persisted.</param>
public sealed record LoginRequest(string TenantSlug, string Email, string Password);
