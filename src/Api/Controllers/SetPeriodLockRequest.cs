namespace Ostagar.Api.Controllers;

/// <summary>
/// Payload for moving one of a fiscal period's three locks.
/// </summary>
/// <param name="Level">Which lock to set: <c>soft</c>, <c>hard</c> or <c>tax</c>.</param>
/// <param name="On">The date the lock runs up to and including. Must fall inside the period; a lock
/// that moves backwards would silently re-open history, so it is refused.</param>
public sealed record SetPeriodLockRequest(string Level, DateOnly On);
