namespace Granit.Identity.Extensions;

/// <summary>
/// Extension methods for reading extra properties from <see cref="IIdentityUser"/>.
/// </summary>
/// <remarks>
/// Available on all <see cref="IIdentityUser"/> implementations:
/// <c>GranitUser</c>, <c>FederatedIdentityUser</c>, and <c>CachedIdentityUser</c>.
/// </remarks>
public static class IdentityUserExtraPropertyExtensions
{
    /// <summary>
    /// Gets an extra property value by name, or <see langword="null"/> if not found.
    /// </summary>
    /// <param name="user">The identity user.</param>
    /// <param name="name">The property name.</param>
    /// <returns>The property value, or <see langword="null"/>.</returns>
    public static string? GetExtraProperty(this IIdentityUser user, string name)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return user.ExtraProperties.GetValueOrDefault(name);
    }

    /// <summary>
    /// Gets an extra property value parsed as <typeparamref name="T"/>, or <see langword="default"/> if not found or unparsable.
    /// </summary>
    /// <typeparam name="T">The target type (must implement <see cref="IParsable{TSelf}"/>).</typeparam>
    /// <param name="user">The identity user.</param>
    /// <param name="name">The property name.</param>
    /// <returns>The parsed value, or <see langword="default"/>.</returns>
    public static T? GetExtraProperty<T>(this IIdentityUser user, string name)
        where T : IParsable<T>
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return user.ExtraProperties.TryGetValue(name, out string? value)
            && T.TryParse(value, null, out T? result)
                ? result
                : default;
    }

    /// <summary>
    /// Returns <see langword="true"/> if the user has an extra property with the given name.
    /// </summary>
    /// <param name="user">The identity user.</param>
    /// <param name="name">The property name.</param>
    /// <returns><see langword="true"/> if the property exists.</returns>
    public static bool HasExtraProperty(this IIdentityUser user, string name)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return user.ExtraProperties.ContainsKey(name);
    }
}
