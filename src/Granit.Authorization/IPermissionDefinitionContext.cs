using Granit.Localization;

namespace Granit.Authorization;

/// <summary>
/// Fluent context for declaring permission groups and permissions during application startup.
/// Multiple providers can add permissions to the same group (GetOrAdd semantics).
/// </summary>
public interface IPermissionDefinitionContext
{
    /// <summary>
    /// Returns the group if it already exists, or creates and registers a new one.
    /// The first provider to declare a group sets its DisplayName.
    /// </summary>
    PermissionGroup AddGroup(string name, LocalizableString? displayName = null);
}
