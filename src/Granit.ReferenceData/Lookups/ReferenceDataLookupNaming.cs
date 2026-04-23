using System.Text;

namespace Granit.ReferenceData.Lookups;

/// <summary>
/// Derives the lookup registry name for a reference data type. Enforces the
/// <c>ref-{kebab-case}</c> convention so every refdata source is discoverable under a
/// predictable prefix (e.g. <c>ref-country</c>, <c>ref-document-types</c>).
/// </summary>
public static class ReferenceDataLookupNaming
{
    /// <summary>
    /// Returns the lookup name for a strongly-typed reference data entity
    /// (e.g. <c>Country</c> → <c>"ref-country"</c>, <c>ProductCategory</c> → <c>"ref-product-category"</c>).
    /// </summary>
    public static string ForEntity(Type entityType)
    {
        ArgumentNullException.ThrowIfNull(entityType);
        return $"ref-{ToKebabCase(entityType.Name)}";
    }

    /// <summary>
    /// Returns the lookup name for a dynamic reference data type registered via
    /// <c>AddReferenceData&lt;TDb&gt;(rd =&gt; rd.Add("DocumentTypes", ...))</c>
    /// (e.g. <c>"DocumentTypes"</c> → <c>"ref-document-types"</c>).
    /// </summary>
    public static string ForTypeName(string typeName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(typeName);
        return $"ref-{ToKebabCase(typeName)}";
    }

    private static string ToKebabCase(string value)
    {
        StringBuilder builder = new(value.Length + 8);
        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];
            if (char.IsUpper(c))
            {
                if (i > 0 && (char.IsLower(value[i - 1]) || (i + 1 < value.Length && char.IsLower(value[i + 1]))))
                {
                    builder.Append('-');
                }
                builder.Append(char.ToLowerInvariant(c));
            }
            else
            {
                builder.Append(c);
            }
        }

        return builder.ToString();
    }
}
