namespace Ostagar.Platform.Auth;

/// <summary>Why a sign-in attempt did not succeed. Each value maps to one translatable message.</summary>
public enum AuthFailureReason
{
    /// <summary>No credential was accepted. Deliberately does not say which part failed.</summary>
    InvalidCredentials,

    /// <summary>The named company does not exist or is not open for business.</summary>
    TenantUnavailable,

    /// <summary>The account exists but has been deactivated.</summary>
    AccountDisabled,

    /// <summary>Too many failed attempts; the account is temporarily locked.</summary>
    AccountLocked,

    /// <summary>The request was missing something required to evaluate it.</summary>
    IncompleteRequest,
}
