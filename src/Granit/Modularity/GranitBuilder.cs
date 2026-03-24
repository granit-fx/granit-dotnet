namespace Granit.Modularity;

/// <summary>
/// Fluent builder for composing Granit modules in <c>Program.cs</c>.
/// Collects module types that are then loaded, deduplicated, and
/// topologically sorted by <see cref="ModuleLoader"/>.
/// </summary>
/// <remarks>
/// This API coexists with <see cref="DependsOnAttribute"/> — modules added via
/// the builder still have their <c>[DependsOn]</c> dependencies resolved automatically.
/// </remarks>
public sealed class GranitBuilder
{
    private readonly HashSet<Type> _moduleTypes = [];

    /// <summary>
    /// Adds a module to the Granit application. Its <c>[DependsOn]</c> dependencies
    /// are resolved automatically.
    /// </summary>
    /// <typeparam name="TModule">The module type to add.</typeparam>
    /// <returns>This builder for chaining.</returns>
    public GranitBuilder AddModule<TModule>() where TModule : GranitModule
    {
        _moduleTypes.Add(typeof(TModule));
        return this;
    }

    /// <summary>
    /// Returns all collected module types. Used internally by the host builder extensions.
    /// </summary>
    internal IReadOnlySet<Type> ModuleTypes => _moduleTypes;
}
