namespace Ostagar.Platform.Audit;

/// <summary>
/// What happened to the action that was attempted. Kept to three values because a fourth would be a
/// report nobody writes: the audit trail exists to answer "who touched this, and were they allowed".
/// </summary>
public enum AuditOutcome
{
    Allowed = 0,
    Denied = 1,
    Failed = 2,
}
