using Ostagar.Core;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Ostagar.Data;

/// <summary>
/// Stores a <see cref="CurrencyCode"/> as its three-letter ISO 4217 text.
/// </summary>
/// <remarks>
/// Reading goes back through <see cref="CurrencyCode.From"/>, so a row holding <c>BD</c>, a lower-case
/// code or an empty string fails on materialisation instead of producing a code that quietly compares
/// unequal to every real currency. That is the failure mode this type exists to prevent, and a converter
/// that accepted anything would reintroduce it one layer down.
///
/// Text and not a smallint ordinal: <see cref="CurrencyCode"/> is an open set validated by shape, and an
/// ordinal would need a table of its own to be readable in a database client — which is where a wrong
/// amount gets found during an audit.
/// </remarks>
public sealed class CurrencyCodeConverter : ValueConverter<CurrencyCode, string>
{
    public CurrencyCodeConverter()
        : base(
            code => code.Value,
            stored => CurrencyCode.From(stored))
    {
    }
}
