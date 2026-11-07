namespace Ostagar.Platform.Identity;

/// <summary>
/// How access tokens are minted and validated.
/// </summary>
/// <remarks>
/// <see cref="SigningKey"/> has no default and no fallback. The legacy tree shipped a JWT secret in
/// <c>appsettings.json</c> and committed it; it was scrubbed from history on 2026-10-15, but the
/// credential was public for the whole time and has to be treated as burned. A host that cannot
/// supply a key must fail at startup rather than invent one, because an invented key is a token
/// anyone can forge.
/// </remarks>
public sealed class AccessTokenOptions
{
    public const string SectionName = "OstagarAuth";

    public required string Issuer { get; init; }

    public required string Audience { get; init; }

    /// <summary>At least 32 bytes of real entropy, supplied by the environment.</summary>
    public required string SigningKey { get; init; }

    public int LifetimeMinutes { get; init; } = 30;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Issuer))
        {
            throw new InvalidOperationException("An access token issuer must be configured.");
        }

        if (string.IsNullOrWhiteSpace(Audience))
        {
            throw new InvalidOperationException("An access token audience must be configured.");
        }

        if (string.IsNullOrWhiteSpace(SigningKey) || SigningKey.Length < 32)
        {
            throw new InvalidOperationException(
                "OstagarAuth:SigningKey must be at least 32 characters of real entropy. It is read from "
                + "the environment and deliberately has no default — a generated or committed key means "
                + "anyone can mint a token for any tenant.");
        }

        if (LifetimeMinutes is < 1 or > 720)
        {
            throw new InvalidOperationException("Access token lifetime must be between 1 and 720 minutes.");
        }
    }
}
