namespace Ostagar.Modules.DocumentWorkflow;

/// <summary>
/// One numbering refusal, named by code and explained in one sentence.
/// </summary>
/// <param name="Code">What rule refused the allocation, for a client to translate or a test to assert on.</param>
/// <param name="Message">English text for logs and support; never the thing a client switches on.</param>
public sealed record NumberingError(NumberingErrorCode Code, string Message);
