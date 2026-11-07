namespace Ostagar.Platform.Failures;

/// <summary>
/// The row the caller named does not exist in the tenant they are scoped to.
/// </summary>
/// <remarks>
/// Deliberately the same answer for "no such row anywhere" and "this row belongs to another company".
/// A difference in the response would be a way to enumerate which identifiers belong to somebody else,
/// and the legacy build had no tenancy column at all, so every identifier in it was globally resolvable.
/// </remarks>
public sealed class NotFoundException : Exception
{
    public NotFoundException()
        : base("The requested record does not exist in this tenant.")
    {
    }

    public NotFoundException(string message)
        : base(message)
    {
    }

    public NotFoundException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
