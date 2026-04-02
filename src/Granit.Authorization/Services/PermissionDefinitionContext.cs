using Granit.Localization;

namespace Granit.Authorization.Services;

/// <summary>
/// Collects permission groups and definitions during application startup.
/// Uses GetOrAdd semantics for groups: multiple providers can add permissions
/// to the same group without conflict.
/// </summary>
internal sealed class PermissionDefinitionContext : IPermissionDefinitionContext
{
    private readonly Dictionary<string, PermissionGroup> _groups = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public PermissionGroup AddGroup(string name, LocalizableString? displayName = null)
    {
        if (_groups.TryGetValue(name, out PermissionGroup? existingGroup))
        {
            return existingGroup;
        }

        PermissionGroup group = new(name, displayName);
        _groups[name] = group;
        return group;
    }

    internal IReadOnlyDictionary<string, PermissionGroup> Groups => _groups;
}
