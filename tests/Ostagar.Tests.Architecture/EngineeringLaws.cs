namespace Ostagar.Tests.Architecture;

/// <summary>
/// The registry of engineering laws enforced on every build. Adding a law here is how a rule in
/// docs/MASTER_PLAN.md stops being a preference.
/// </summary>
internal static class EngineeringLaws
{
    internal static IReadOnlyList<EngineeringLaw> All { get; } =
    [
        new OneTypePerFileLaw(),
        new FileNameMatchesDeclaredTypeLaw(),
        new NoCommentedOutRegistrationLaw(),
        new NoFakeSuccessLaw(),
        new ProductionProjectPurityLaw(),
        new ExplicitAuthorizationLaw(),
        new PersistenceBelongsToOneProjectLaw(),
    ];

    internal static EngineeringLaw ById(string lawId) =>
        All.FirstOrDefault(law => string.Equals(law.Id, lawId, StringComparison.Ordinal))
        ?? throw new ArgumentException($"No engineering law is registered under '{lawId}'.", nameof(lawId));
}
