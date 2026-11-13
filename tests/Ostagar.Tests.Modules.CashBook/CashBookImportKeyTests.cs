using Ostagar.Modules.CashBook;

namespace Ostagar.Tests.Modules.CashBook;

public sealed class CashBookImportKeyTests
{
    [Fact]
    public void The_same_file_and_row_derives_the_same_id_which_is_what_makes_a_reupload_safe()
    {
        var fingerprint = CashBookImportKey.Fingerprint("a,csv,file");

        var first = CashBookImportKey.TransactionId(fingerprint, 4, CashTransactionKind.Receipt);
        var again = CashBookImportKey.TransactionId(fingerprint, 4, CashTransactionKind.Receipt);

        Assert.Equal(first, again);
    }

    [Fact]
    public void A_different_line_or_side_is_a_different_transaction()
    {
        var fingerprint = CashBookImportKey.Fingerprint("a,csv,file");

        var receipt = CashBookImportKey.TransactionId(fingerprint, 4, CashTransactionKind.Receipt);
        var payment = CashBookImportKey.TransactionId(fingerprint, 4, CashTransactionKind.Payment);
        var nextLine = CashBookImportKey.TransactionId(fingerprint, 5, CashTransactionKind.Receipt);

        Assert.NotEqual(receipt, payment);
        Assert.NotEqual(receipt, nextLine);
    }

    [Fact]
    public void An_edited_file_is_a_new_import_because_changed_bytes_are_different_content()
    {
        var first = CashBookImportKey.TransactionId(
            CashBookImportKey.Fingerprint("a,csv,file"), 4, CashTransactionKind.Receipt);
        var edited = CashBookImportKey.TransactionId(
            CashBookImportKey.Fingerprint("a,csv,file,edited"), 4, CashTransactionKind.Receipt);

        Assert.NotEqual(first, edited);
    }
}
