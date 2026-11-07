namespace Ostagar.Platform.Failures;

/// <summary>
/// The request was well-formed and authorised, but the business rule refused it: a duplicate code, a
/// system role that cannot be deleted, a change made against a stale copy of the row.
/// </summary>
public sealed class RuleViolationException : Exception
{
    public RuleViolationException()
        : base("The change was refused.")
    {
    }

    public RuleViolationException(string message)
        : base(message)
    {
    }

    public RuleViolationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
