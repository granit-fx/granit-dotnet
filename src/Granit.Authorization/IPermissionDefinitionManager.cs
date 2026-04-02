namespace Granit.Authorization;

/// <summary>
/// Singleton aggregator of all <see cref="IPermissionDefinitionProvider"/> registrations.
/// Provides lookup and enumeration of all declared permissions at runtime.
/// </summary>
public interface IPermissionDefinitionManager
{
    /// <summary>Returns true if a permission with this exact name has been declared.</summary>
    bool Exists(string name);

    /// <summary>Returns the definition for the given name, or null if not declared.</summary>
    PermissionDefinition? Find(string name);

    /// <summary>Returns all declared permissions across all providers and groups.</summary>
    IReadOnlyList<PermissionDefinition> GetAll();

    /// <summary>Returns all declared permission groups.</summary>
    IReadOnlyList<PermissionGroup> GetGroups();
}
