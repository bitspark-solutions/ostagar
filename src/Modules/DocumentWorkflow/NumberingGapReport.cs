namespace Ostagar.Modules.DocumentWorkflow;

/// <summary>
/// What the numbers a tenant believes were issued in one period actually look like.
/// </summary>
/// <remarks>
/// Gaplessness is claimed by the allocator, and a claim is not evidence — the legacy tree also claimed
/// gapless voucher numbers while ordering them as text, which is how <c>JE-2025-01-9</c> appeared after
/// <c>JE-2025-01-10</c> and looked like a missing file. This report is the checkable version: hand it the
/// numbers on the documents of one period, cancelled ones included, and it says whether the series is
/// whole. <see cref="MissingRanges"/> is the finding an auditor asks about;
/// <see cref="UnreadableNumbers"/> is the finding that somebody typed a number in by hand.
/// </remarks>
/// <param name="IsGapless">Every value from the series start to the highest issued value is present exactly
/// once, and every number read back cleanly. Vacuously true for a period with no documents.</param>
/// <param name="MissingRanges">Spans no document holds, ascending.</param>
/// <param name="DuplicateNumbers">Numbers carried by more than one document — the failure the row lock and
/// the unique index are each supposed to make impossible, so a hit here is a real incident.</param>
/// <param name="UnreadableNumbers">Strings this scheme could not have rendered.</param>
/// <param name="LowestValue">Lowest counter value found, or zero when none was.</param>
/// <param name="HighestValue">Highest counter value found, or zero when none was.</param>
public sealed record NumberingGapReport(
    bool IsGapless,
    IReadOnlyList<NumberingValueRange> MissingRanges,
    IReadOnlyList<string> DuplicateNumbers,
    IReadOnlyList<string> UnreadableNumbers,
    int LowestValue,
    int HighestValue);
