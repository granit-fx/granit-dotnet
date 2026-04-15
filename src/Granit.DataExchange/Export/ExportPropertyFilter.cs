using System.Collections;
using System.Reflection;
using Granit.DataProtection;

namespace Granit.DataExchange.Export;

/// <summary>
/// Builds <see cref="ExportFieldDescriptor"/> lists by introspecting an entity type's
/// public readable properties, filtering out sensitive, binary, and collection fields.
/// </summary>
/// <remarks>
/// <para>
/// Used by <see cref="ReflectionExportDefinition"/> to provide automatic export
/// coverage for entities that lack an explicit <see cref="ExportDefinition{TEntity}"/>.
/// </para>
/// <para>
/// Security filtering reuses existing framework attributes — no dedicated
/// <c>[ExportIgnore]</c> attribute is needed:
/// <list type="bullet">
///   <item><see cref="SensitiveDataAttribute"/> with <see cref="SensitiveDataMode.Omit"/> → excluded</item>
///   <item><see cref="SensitiveDataAttribute"/> with <see cref="Sensitivity.Restricted"/> → excluded</item>
///   <item><c>[AuditIgnore]</c> (checked by type name — no <c>Granit.Auditing</c> reference) → excluded</item>
///   <item><c>[Encrypted(KeyIsolation = true)]</c> (checked by type name — no <c>Granit.Encryption</c> reference) → excluded</item>
/// </list>
/// </para>
/// </remarks>
internal static class ExportPropertyFilter
{
    private static readonly HashSet<string> InfrastructurePropertyNames =
        new(StringComparer.Ordinal) { "ConcurrencyStamp", "SecurityStamp" };

    private static readonly HashSet<Type> ExportableTypes =
    [
        typeof(string),
        typeof(bool),
        typeof(char),
        typeof(byte),
        typeof(short),
        typeof(int),
        typeof(long),
        typeof(float),
        typeof(double),
        typeof(decimal),
        typeof(Guid),
        typeof(DateTime),
        typeof(DateTimeOffset),
        typeof(DateOnly),
        typeof(TimeOnly),
    ];

    /// <summary>
    /// Builds field descriptors for all exportable properties of the given entity type.
    /// </summary>
    public static IReadOnlyList<ExportFieldDescriptor> BuildFields(Type entityType)
    {
        ArgumentNullException.ThrowIfNull(entityType);

        PropertyInfo[] properties = entityType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
        List<ExportFieldDescriptor> fields = [];
        int order = 0;

        foreach (PropertyInfo prop in properties)
        {
            if (!prop.CanRead)
            {
                continue;
            }

            if (IsExcluded(prop))
            {
                continue;
            }

            Type propType = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;

            if (!IsExportableType(propType))
            {
                continue;
            }

            fields.Add(new ExportFieldDescriptor(
                PropertyPath: prop.Name,
                ClrTypeName: propType.Name,
                Header: null,
                Format: null,
                Order: order++,
                IsNavigation: false));
        }

        return fields.AsReadOnly();
    }

    private static bool IsExportableType(Type type) =>
        ExportableTypes.Contains(type) || type.IsEnum;

    private static bool IsExcluded(PropertyInfo prop)
    {
        // 1. Infrastructure property names
        if (InfrastructurePropertyNames.Contains(prop.Name))
        {
            return true;
        }

        // 2. Collections (except string which implements IEnumerable)
        if (prop.PropertyType != typeof(string) &&
            typeof(IEnumerable).IsAssignableFrom(prop.PropertyType))
        {
            return true;
        }

        // 3. Binary data
        if (prop.PropertyType == typeof(byte[]))
        {
            return true;
        }

        // 4. JSON blobs (checked by type name — no System.Text.Json.Nodes reference needed)
        string typeName = prop.PropertyType.Name;
        if (typeName is "JsonDocument" or "JsonElement")
        {
            return true;
        }

        // 5. [SensitiveData] with Omit mode or Restricted level (direct attribute check)
        SensitiveDataAttribute? sensitiveAttr = prop.GetCustomAttribute<SensitiveDataAttribute>();
        if (sensitiveAttr is not null &&
            (sensitiveAttr.Mode == SensitiveDataMode.Omit || sensitiveAttr.Level == Sensitivity.Restricted))
        {
            return true;
        }

        // 6. [AuditIgnore] — checked by type name (no Granit.Auditing dependency)
        if (HasAttributeByName(prop, "AuditIgnoreAttribute"))
        {
            return true;
        }

        // 7. [Encrypted(KeyIsolation = true)] — checked by type name + reflection
        if (HasEncryptedWithKeyIsolation(prop))
        {
            return true;
        }

        return false;
    }

    private static bool HasAttributeByName(PropertyInfo prop, string attributeTypeName) =>
        prop.GetCustomAttributes(inherit: true)
            .Any(attr => attr.GetType().Name == attributeTypeName);

    private static bool HasEncryptedWithKeyIsolation(PropertyInfo prop)
    {
        object? encryptedAttr = prop.GetCustomAttributes(inherit: true)
            .FirstOrDefault(attr => attr.GetType().Name == "EncryptedAttribute");

        if (encryptedAttr is null)
        {
            return false;
        }

        // Read KeyIsolation property via reflection (no Granit.Encryption.EntityFrameworkCore ref)
        PropertyInfo? keyIsolationProp = encryptedAttr.GetType().GetProperty("KeyIsolation");
        if (keyIsolationProp is null)
        {
            return false;
        }

        return keyIsolationProp.GetValue(encryptedAttr) is true;
    }
}
