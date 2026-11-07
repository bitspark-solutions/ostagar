namespace Ostagar.Api.Controllers;

/// <summary>
/// A general-ledger entry as the ledger holds it, including the link that makes it a correction.
/// </summary>
/// <param name="Id">Entry identifier.</param>
/// <param name="Number">The gapless journal number, shown because it is what an auditor cites.</param>
/// <param name="JournalCode">Which journal assigned that number.</param>
/// <param name="PostingDate">Effective date.</param>
/// <param name="PeriodId">The period the entry sits in.</param>
/// <param name="Status">Draft or Posted. There is deliberately no "Reversed" status: a posted entry never
/// changes, and <see cref="ReversesEntryId"/> on the newer entry is what records the correction.</param>
/// <param name="ReversesEntryId">When set, this entry corrects that one.</param>
/// <param name="ReversedByEntryId">When set, a later entry has corrected this one. Resolved on read so a
/// caller does not have to search the ledger for its own history.</param>
/// <param name="Memo">The entry's own note.</param>
/// <param name="TotalDebit">Sum of the debit side.</param>
/// <param name="TotalCredit">Sum of the credit side. Equal to <see cref="TotalDebit"/> for any Posted
/// entry, by the database's rule and not this response's hope.</param>
/// <param name="Lines">The entry's lines in order.</param>
public sealed record JournalEntryResponse(
    Guid Id,
    string Number,
    string JournalCode,
    DateOnly PostingDate,
    Guid PeriodId,
    string Status,
    Guid? ReversesEntryId,
    Guid? ReversedByEntryId,
    string Memo,
    decimal TotalDebit,
    decimal TotalCredit,
    IReadOnlyList<JournalLineResponse> Lines);
