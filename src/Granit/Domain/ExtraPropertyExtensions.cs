using System.Collections.ObjectModel;
using System.Text.Json;

namespace Granit.Domain;

/// <summary>
/// Extension methods for reading and writing extra properties on <see cref="IHasExtraProperties"/> entities.
/// </summary>
/// <remarks>
/// <para>
/// Extra properties are stored as a JSON dictionary in <see cref="IHasExtraProperties.ExtraPropertiesJson"/>.
/// These methods provide typed access without requiring the consumer to handle JSON serialization.
/// </para>
/// <para>
/// When a property has been promoted to a real SQL column via <c>MapProperty&lt;T&gt;</c>,
/// the <c>ExtraPropertySyncInterceptor</c> ensures it is excluded from the JSON bag
/// at save time to prevent data duplication.
/// </para>
/// </remarks>
public static class ExtraPropertyExtensions
{
    /// <summary>
    /// Gets all extra properties as a read-only dictionary.
    /// </summary>
    /// <param name="entity">The entity to read from.</param>
    /// <returns>A read-only dictionary of extra properties. Never <see langword="null"/>.</returns>
    public static IReadOnlyDictionary<string, string> GetExtraProperties(this IHasExtraProperties entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        if (string.IsNullOrWhiteSpace(entity.ExtraPropertiesJson))
        {
            return ReadOnlyDictionary<string, string>.Empty;
        }

        Dictionary<string, string>? parsed =
            JsonSerializer.Deserialize<Dictionary<string, string>>(entity.ExtraPropertiesJson);

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
    public static string? GetExtraProperty(this IHasExtraProperties entity, string name)
    {
        ArgumentNullException.ThrowIfNull(entity);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return entity.GetExtraProperties().GetValueOrDefault(name);
    }

    /// <summary>
    /// Gets an extra property value parsed as <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">A type implementing <see cref="IParsable{TSelf}"/>.</typeparam>
    /// <param name="entity">The entity to read from.</param>
    /// <param name="name">The property name.</param>
    /// <returns>The parsed value, or <see langword="default"/> if not found or unparseable.</returns>
    public static T? GetExtraProperty<T>(this IHasExtraProperties entity, string name)
        where T : IParsable<T>
    {
        ArgumentNullException.ThrowIfNull(entity);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        string? value = entity.GetExtraProperty(name);
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
    public static void SetExtraProperty(this IHasExtraProperties entity, string name, string? value)
    {
        ArgumentNullException.ThrowIfNull(entity);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        Dictionary<string, string> props = string.IsNullOrWhiteSpace(entity.ExtraPropertiesJson)
            ? []
            : JsonSerializer.Deserialize<Dictionary<string, string>>(entity.ExtraPropertiesJson) ?? [];

        if (value is null)
        {
            props.Remove(name);
        }
        else
        {
            props[name] = value;
        }

        entity.ExtraPropertiesJson = props.Count > 0 ? JsonSerializer.Serialize(props) : null;
    }

    /// <summary>
    /// Checks whether an extra property exists.
    /// </summary>
    /// <param name="entity">The entity to check.</param>
    /// <param name="name">The property name.</param>
    /// <returns><see langword="true"/> if the property exists; otherwise <see langword="false"/>.</returns>
    public static bool HasExtraProperty(this IHasExtraProperties entity, string name)
    {
        ArgumentNullException.ThrowIfNull(entity);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return entity.GetExtraProperties().ContainsKey(name);
    }
}
