namespace Ostagar.Modules.CashBook;

/// <summary>
/// Machine-readable reasons one CSV line, or one side of it, cannot become a cash transaction.
/// </summary>
/// <remarks>
/// Codes rather than prose for the same reason <see cref="CashTransactionErrorCode"/> exists: a client
/// renders the message in its own language and a test asserts on the code. The legacy importer's
/// answer to a malformed row was <c>continue</c> — the file reported success with fewer rows than it
/// contained, and the missing rows were discovered at tie-out, which is the worst possible time.
/// </remarks>
public enum CashBookImportErrorCode
{
    /// <summary>
    /// No header line was found. The client book's header carries a misspelt "Catagories" column and
    /// both spellings are accepted; neither present means this is some other CSV, and guessing at the
    /// column order of a different file is how money lands in the wrong account.
    /// </summary>
    FileHeaderMissing,

    /// <summary>Fewer columns than the two-sided layout needs. Reported, not skipped.</summary>
    RowTooShort,

    /// <summary>A side that has any content must have a readable date.</summary>
    DateUnparseable,

    /// <summary>A side that has any content must have a readable, positive amount.</summary>
    AmountUnparseable,

    /// <summary>
    /// Zero or negative. The direction is stated by which half of the page the row sits on, the same
    /// rule <see cref="CashTransactionErrorCode.AmountNotPositive"/> applies to the manual form.
    /// </summary>
    AmountNotPositive,

    /// <summary>A side with a date or amount but no category — the account the movement is for is unknown.</summary>
    CategoryMissing,

    /// <summary>
    /// The category is not in the caller's explicit category→account mapping. Refused rather than
    /// auto-created: the legacy import invented CoA accounts from free text with
    /// <c>new Random().Next(100,999)</c> codes, which is precisely the behaviour the master plan's P3
    /// gate exists to remove.
    /// </summary>
    CategoryUnmapped,
}
