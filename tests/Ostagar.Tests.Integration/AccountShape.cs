namespace Ostagar.Tests.Integration;

/// <summary>
/// One account as the database holds it, for asserting the shape of a provisioned chart.
/// </summary>
/// <remarks>
/// Path, parent and id are read straight from the row rather than re-derived from the template, so a
/// test can catch a provisioning run that wrote the right accounts into the wrong hierarchy — or the
/// same account twice under two ids.
/// </remarks>
internal sealed record AccountShape(
    Guid Id,
    string Code,
    string Path,
    Guid? ParentId,
    bool IsPostable,
    bool IsActive);
