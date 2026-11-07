namespace Ostagar.Platform.Failures;

/// <summary>
/// Credentials did not open a session. One message for wrong password, unknown user, inactive user and
/// inactive tenant, because a login endpoint that distinguishes them is a user-enumeration oracle.
/// </summary>
public sealed class InvalidCredentialsException : Exception
{
    public InvalidCredentialsException()
        : base("Those credentials are not valid for this tenant.")
    {
    }

    public InvalidCredentialsException(string message)
        : base(message)
    {
    }

    public InvalidCredentialsException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
