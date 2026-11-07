namespace Ostagar.Platform.Failures;

/// <summary>
/// Too many failed attempts for this login. Reported as a refusal rather than a delay, because the
/// response a client gets must not suggest the password was the problem.
/// </summary>
public sealed class AccountLockedException : Exception
{
    public AccountLockedException()
        : base("This login is temporarily locked after repeated failed attempts.")
    {
    }

    public AccountLockedException(string message)
        : base(message)
    {
    }

    public AccountLockedException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
