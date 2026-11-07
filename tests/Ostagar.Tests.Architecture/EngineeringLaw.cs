namespace Ostagar.Tests.Architecture;

/// <summary>
/// A repository-wide engineering law from the master plan, expressed as something that can be
/// checked rather than remembered. A law with no checker is a preference, not a law.
/// </summary>
internal interface EngineeringLaw
{
    /// <summary>Plan law identifier, e.g. <c>L1</c>.</summary>
    string Id { get; }

    /// <summary>One sentence stating the rule and where it is written down.</summary>
    string Rule { get; }

    /// <summary>All current breaches. Empty means the law holds.</summary>
    IEnumerable<Violation> FindViolations();
}
