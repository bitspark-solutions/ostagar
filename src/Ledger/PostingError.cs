namespace Ostagar.Ledger;

/// <summary>One reason a proposed posting was refused, pointing at the offending line where there is one.</summary>
/// <param name="Code">Machine-readable classification.</param>
/// <param name="Message">Human-readable explanation, for logs; the UI translates from <paramref name="Code"/>.</param>
/// <param name="LineIndex">Zero-based index of the offending line, or null when the problem concerns the whole posting.</param>
public sealed record PostingError(PostingErrorCode Code, string Message, int? LineIndex = null);
