using System.Text;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore;

namespace Ostagar.Data;

/// <summary>
/// Rewrites every column name in a model to snake_case.
/// </summary>
/// <remarks>
/// Shared because more than one context maps the same physical tables. Column naming that lives in a
/// single context would let a second context drift to <c>"TenantId"</c> while the first wrote
/// <c>tenant_id</c>, and the row-level security policies name the column literally.
/// </remarks>
public static class SnakeCaseModelNames
{
    public static void Apply(ModelBuilder builder)
    {
        foreach (var entityType in builder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                property.SetColumnName(ToSnakeCase(property.GetColumnName()));
            }
        }
    }

    public static string ToSnakeCase(string value)
    {
        if (value.Length == 0 || !ContainsUpper(value))
        {
            return value;
        }

        var buffer = new StringBuilder(value.Length + 8);
        for (var index = 0; index < value.Length; index++)
        {
            var current = value[index];

            // A capital that follows a lowercase letter, or that starts an acronym followed by a word,
            // begins a new word: ApplicationUserId -> application_user_id.
            var startsWord = char.IsUpper(current)
                && index > 0
                && (!char.IsUpper(value[index - 1]) || (index + 1 < value.Length && !char.IsUpper(value[index + 1])));

            if (startsWord)
            {
                buffer.Append('_');
            }

            buffer.Append(char.ToLowerInvariant(current));
        }

        return buffer.ToString();
    }

    private static bool ContainsUpper(string value)
    {
        foreach (var character in value)
        {
            if (char.IsUpper(character))
            {
                return true;
            }
        }

        return false;
    }
}
