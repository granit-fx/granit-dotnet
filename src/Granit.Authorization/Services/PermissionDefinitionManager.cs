
namespace Granit.Authorization.Services;

/// <summary>
/// Singleton that aggregates all <see cref="IPermissionDefinitionProvider"/> registrations
/// into a flat lookup dictionary. Built at first resolution (lazy singleton via DI).
/// </summary>
internal sealed class PermissionDefinitionManager : IPermissionDefinitionManager
{
    private readonly IReadOnlyDictionary<string, PermissionDefinition> _permissions;
    private readonly IReadOnlyList<PermissionGroup> _groups;

    public PermissionDefinitionManager(IEnumerable<IPermissionDefinitionProvider> providers)
    {
        PermissionDefinitionContext context = new();
        foreach (IPermissionDefinitionProvider provider in providers)
        {
            provider.DefinePermissions(context);
        }

        _groups = [.. context.Groups.Values];
        _permissions = context.Groups.Values
            .SelectMany(g => g.Permissions)
            .ToDictionary(p => p.Name, StringComparer.Ordinal);
    }

    /// <inheritdoc />
    public bool Exists(string name) => _permissions.ContainsKey(name);

    /// <inheritdoc />
    public PermissionDefinition? Find(string name) => _permissions.GetValueOrDefault(name);

    /// <inheritdoc />
    public IReadOnlyList<PermissionDefinition> GetAll() => [.. _permissions.Values];

    /// <inheritdoc />
    public IReadOnlyList<PermissionGroup> GetGroups() => _groups;
}
