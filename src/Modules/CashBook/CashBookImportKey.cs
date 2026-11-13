using System.Security.Cryptography;
using System.Text;

namespace Ostagar.Modules.CashBook;

/// <summary>
/// The deterministic identity of one imported row-side, which is what makes re-uploading a file safe.
/// </summary>
/// <remarks>
/// <para>
/// A hand-keyed transaction gets a fresh id and relies on the poster's retry window; a file has no
/// retry window — it is uploaded again, wholesale, after the first attempt half-worked. So an imported
/// row's identity is derived from what it is rather than assigned: the SHA-256 of the file content,
/// the line number, and which side of the row it is. Uploading the same bytes answers "already
/// posted" for every row instead of posting them twice, which is invariant I5 applied to a file.
/// </para>
///
/// <para>
/// The consequence is stated plainly: editing the file and re-uploading posts the changed file as new
/// transactions, because a different fingerprint is genuinely different content. Deduplication is per
/// file, not per semantic transaction — a row that appears in two overlapping exports will post twice,
/// and correcting that is a reversal, not a smarter hash.
/// </para>
/// </remarks>
public static class CashBookImportKey
{
    /// <summary>Versioned domain separator, so the same input can never collide with another
    /// feature's derived ids.</summary>
    private const string Scope = "ostagar.cash.import.v1";

    /// <summary>The file's content fingerprint. Two uploads of identical bytes are the same import.</summary>
    public static byte[] Fingerprint(string content) =>
        SHA256.HashData(Encoding.UTF8.GetBytes(content));

    /// <summary>
    /// The document id (and therefore the posting's <c>SourceId</c>) for one side of one line.
    /// A version-5-shaped name-based GUID: deterministic per (file, line, side), still a well-formed
    /// UUID for every consumer downstream.
    /// </summary>
    public static Guid TransactionId(byte[] fingerprint, int lineNumber, CashTransactionKind kind)
    {
        var name = string.Create(
            CultureScope,
            $"{Scope}|{Convert.ToHexString(fingerprint)}|{lineNumber}|{(int)kind}");

        var hash = SHA256.HashData(Encoding.ASCII.GetBytes(name));
        var bytes = new byte[16];
        Array.Copy(hash, bytes, bytes.Length);

        // RFC 4122 name-based shape: version 5, variant 1. Cosmetic but cheap — tools that introspect
        // GUIDs then read these correctly as derived ids rather than random ones.
        bytes[6] = (byte)((bytes[6] & 0x0F) | 0x50);
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);

        return new Guid(bytes);
    }

    private static readonly System.Globalization.CultureInfo CultureScope =
        System.Globalization.CultureInfo.InvariantCulture;
}
