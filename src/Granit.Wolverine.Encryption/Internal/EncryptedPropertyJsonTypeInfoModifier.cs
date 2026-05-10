using System.Reflection;
using System.Text.Json.Serialization.Metadata;
using Granit.Encryption;

namespace Granit.Wolverine.Encryption.Internal;

/// <summary>
/// <see cref="IJsonTypeInfoResolver"/> modifier that walks every type info and
/// attaches <see cref="EncryptedStringJsonConverter"/> to every <see cref="string"/>
/// property carrying <see cref="EncryptedAttribute"/>.
/// </summary>
/// <remarks>
/// Designed to be plugged into the chain configured by
/// <c>WolverineOptions.UseSystemTextJsonForSerialization()</c>. Runs once per
/// type at metadata-build time; the converter instance is captured once and
/// reused across all properties.
/// </remarks>
internal sealed class EncryptedPropertyJsonTypeInfoModifier(IStringEncryptionService encryption)
{
    private readonly EncryptedStringJsonConverter _converter = new(encryption);

    public void Modify(JsonTypeInfo typeInfo)
    {
        ArgumentNullException.ThrowIfNull(typeInfo);

        if (typeInfo.Kind != JsonTypeInfoKind.Object)
        {
            return;
        }

        foreach (JsonPropertyInfo property in typeInfo.Properties)
        {
            if (property.PropertyType != typeof(string))
            {
                continue;
            }

            // PropertyInfo? exposes the source-of-truth metadata for non-record
            // properties; for positional records it's the constructor parameter.
            if (property.AttributeProvider is not ICustomAttributeProvider provider)
            {
                continue;
            }

            object[] attrs = provider.GetCustomAttributes(typeof(EncryptedAttribute), inherit: true);
            if (attrs.Length == 0)
            {
                continue;
            }

            // KeyIsolation is an EF-Core-only feature (per-entity key in
            // IEntityEncryptionKeyStore); the JSON path uses the shared key only.
            // Properties with KeyIsolation = true on a type that flows through
            // Wolverine still get the shared-key encryption — preserves PII at
            // rest in the outbox while the EF interceptor handles the per-entity
            // case at the database layer.
            property.CustomConverter = _converter;
        }
    }
}
