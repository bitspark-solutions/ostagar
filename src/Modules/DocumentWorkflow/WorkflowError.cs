namespace Ostagar.Modules.DocumentWorkflow;

/// <summary>
/// One workflow refusal or definition defect, named by code and explained in English.
/// </summary>
/// <param name="Code">The rule that refused, which is what a caller switches on and translates.</param>
/// <param name="Message">A sentence for logs and support tickets. Never compared against.</param>
public sealed record WorkflowError(WorkflowErrorCode Code, string Message);
