namespace Ostagar.Modules.Partners;

/// <summary>
/// What a duplicate check concluded, in three answers rather than two.
/// </summary>
/// <remarks>
/// <see cref="SuspectedDuplicate"/> exists because the identifiers a factory holds are not equally reliable.
/// Collapsing "might be the same party" into either yes or no is the mistake in both directions: refusing on a
/// shared phone number blocks a real business and teaches the clerk to open the record under a misspelling,
/// while accepting a second trade licence silently splits one buyer's balance into two ageing lines, which no
/// report flags and the owner discovers when they chase money they think they are owed.
///
/// A suspected duplicate is therefore an unresolved state a person must clear, not a warning that can be
/// logged and ignored.
/// </remarks>
public enum DuplicateVerdict
{
    /// <summary>No identifier in common with any existing partner.</summary>
    Unique,

    /// <summary>An authoritative identifier is already held by another partner. The record is refused.</summary>
    ConfirmedDuplicate,

    /// <summary>A suggestive identifier is shared. Creation is held until someone decides which party this is.</summary>
    SuspectedDuplicate,
}
