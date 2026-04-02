namespace Granit.Authorization.Abstractions;

/// <summary>
/// Implemented by application modules to declare the permissions they expose.
/// Register implementations in DI as <see cref="IPermissionDefinitionProvider"/> (Singleton).
/// </summary>
public interface IPermissionDefinitionProvider
{
    /// <summary>Declares all permission groups and permissions for this module.</summary>
    void DefinePermissions(IPermissionDefinitionContext context);
}
