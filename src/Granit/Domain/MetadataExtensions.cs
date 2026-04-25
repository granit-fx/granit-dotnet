using System.Collections.ObjectModel;
using System.Text.Json;

namespace Granit.Domain;

/// <summary>
/// Extension methods for reading and writing extra properties on <see cref="IHasMetadata"/> entities.
/// </summary>
/// <remarks>
/// <para>
/// Extra properties are stored as a JSON dictionary in <see cref="IHasMetadata.MetadataJson"/>.
/// These methods provide typed access without requiring the consumer to handle JSON serialization.
/// </para>
/// <para>
/// When a property has been promoted to a real SQL column via <c>MapProperty&lt;T&gt;</c>,
/// the <c>MetadataSyncInterceptor</c> ensures it is excluded from the JSON bag
/// at save time to prevent data duplication.
/// </para>
/// </remarks>
public static class MetadataExtensions
{
    /// <summary>
    /// Gets all extra properties as a read-only dictionary.
    /// </summary>
    /// <param name="entity">The entity to read from.</param>
    /// <returns>A read-only dictionary of extra properties. Never <see langword="null"/>.</returns>
    public static IReadOnlyDictionary<string, string> GetMetadata(this IHasMetadata entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        if (string.IsNullOrWhiteSpace(entity.MetadataJson))
        {
            return ReadOnlyDictionary<string, string>.Empty;
        }

        Dictionary<string, string>? parsed =
            JsonSerializer.Deserialize<Dictionary<string, string>>(entity.MetadataJson);

        return parsed is { Count: > 0 }
            ? new ReadOnlyDictionary<string, string>(parsed)
            : ReadOnlyDictionary<string, string>.Empty;
    }

    /// <summary>
    /// Gets an extra property value by name.
    /// </summary>
    /// <param name="entity">The entity to read from.</param>
    /// <param name="name">The property name.</param>
    /// <returns>The property value, or <see langword="null"/> if not found.</returns>
    public static string? GetMetadataValue(this IHasMetadata entity, string name)
    {
        ArgumentNullException.ThrowIfNull(entity);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return entity.GetMetadata().GetValueOrDefault(name);
    }

    /// <summary>
    /// Gets an extra property value parsed as <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">A type implementing <see cref="IParsable{TSelf}"/>.</typeparam>
    /// <param name="entity">The entity to read from.</param>
    /// <param name="name">The property name.</param>
    /// <returns>The parsed value, or <see langword="default"/> if not found or unparseable.</returns>
    public static T? GetMetadataValue<T>(this IHasMetadata entity, string name)
        where T : IParsable<T>
    {
        ArgumentNullException.ThrowIfNull(entity);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        string? value = entity.GetMetadataValue(name);
        return value is not null && T.TryParse(value, null, out T? result)
            ? result
            : default;
    }

    /// <summary>
    /// Sets an extra property. Pass <see langword="null"/> to remove the property.
    /// </summary>
    /// <param name="entity">The entity to modify.</param>
    /// <param name="name">The property name.</param>
    /// <param name="value">The property value, or <see langword="null"/> to remove.</param>
    public static void SetMetadataValue(this IHasMetadata entity, string name, string? value)
    {
        ArgumentNullException.ThrowIfNull(entity);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        Dictionary<string, string> props = string.IsNullOrWhiteSpace(entity.MetadataJson)
            ? []
            : JsonSerializer.Deserialize<Dictionary<string, string>>(entity.MetadataJson) ?? [];

        if (value is null)
        {
            props.Remove(name);
        }
        else
        {
            props[name] = value;
        }

        entity.MetadataJson = props.Count > 0 ? JsonSerializer.Serialize(props) : null;
    }

    /// <summary>
    /// Checks whether an extra property exists.
    /// </summary>
    /// <param name="entity">The entity to check.</param>
    /// <param name="name">The property name.</param>
    /// <returns><see langword="true"/> if the property exists; otherwise <see langword="false"/>.</returns>
    public static bool HasMetadataValue(this IHasMetadata entity, string name)
    {
        ArgumentNullException.ThrowIfNull(entity);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return entity.GetMetadata().ContainsKey(name);
    }
}
