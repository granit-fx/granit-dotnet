using Granit.Localization;

namespace Granit.Authorization.Abstractions;

/// <summary>Groups related permissions for organizational purposes.</summary>
public sealed class PermissionGroup(string name, LocalizableString? displayName = null)
{
    private readonly List<PermissionDefinition> _permissions = [];

    /// <summary>Unique group name used as a namespace prefix for permission names.</summary>
    public string Name { get; } = name;

    /// <summary>Localizable display name for admin UI.</summary>
    public LocalizableString? DisplayName { get; } = displayName;

    /// <summary>All permissions defined in this group.</summary>
    public IReadOnlyList<PermissionDefinition> Permissions => _permissions.AsReadOnly();

    /// <summary>Adds a permission to this group and returns its definition.</summary>
    public PermissionDefinition AddPermission(string name, LocalizableString? displayName = null)
    {
        PermissionDefinition definition = new(name, displayName, Name);
        _permissions.Add(definition);
        return definition;
    }
}
