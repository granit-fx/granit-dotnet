namespace Granit.Modularity;

/// <summary>
/// Discovers, instantiates, and topologically sorts Granit modules
/// starting from a root module.
/// </summary>
internal static class ModuleLoader
{
    /// <summary>
    /// Loads all modules reachable from <typeparamref name="TModule"/>
    /// and returns them in topological order (dependencies first).
    /// </summary>
    /// <exception cref="InvalidOperationException">Circular dependency detected.</exception>
    public static IReadOnlyList<ModuleDescriptor> LoadModules<TModule>()
        where TModule : GranitModule =>
        LoadModules(typeof(TModule));

    /// <summary>
    /// Loads all modules reachable from <paramref name="startupModuleType"/>
    /// and returns them in topological order (dependencies first).
    /// </summary>
    public static IReadOnlyList<ModuleDescriptor> LoadModules(Type startupModuleType)
    {
        Dictionary<Type, ModuleDescriptor> descriptors = [];
        DiscoverModules(startupModuleType, descriptors);
        return TopologicalSort(descriptors);
    }

    /// <summary>
    /// Loads all modules reachable from multiple root module types
    /// and returns them in topological order (dependencies first, deduplicated).
    /// </summary>
    public static IReadOnlyList<ModuleDescriptor> LoadModules(IEnumerable<Type> moduleTypes)
    {
        Dictionary<Type, ModuleDescriptor> descriptors = [];
        foreach (Type moduleType in moduleTypes)
        {
            DiscoverModules(moduleType, descriptors);
        }

        return TopologicalSort(descriptors);
    }

    private static void DiscoverModules(Type moduleType, Dictionary<Type, ModuleDescriptor> descriptors)
    {
        if (descriptors.ContainsKey(moduleType))
        {
            return;
        }

        if (!typeof(GranitModule).IsAssignableFrom(moduleType))
        {
            throw new InvalidOperationException(
                $"Type '{moduleType.FullName}' does not inherit from GranitModule.");
        }

        var instance = (GranitModule)Activator.CreateInstance(moduleType)!;

        Type[] dependencies = [.. moduleType
            .GetCustomAttributes(typeof(DependsOnAttribute), true)
            .Cast<DependsOnAttribute>()
            .SelectMany(a => a.DependedTypes)
            .Distinct()];

        descriptors[moduleType] = new ModuleDescriptor(moduleType, instance, dependencies);

        foreach (Type dep in dependencies)
        {
            DiscoverModules(dep, descriptors);
        }
    }

    /// <summary>
    /// Topological sort using Kahn's algorithm.
    /// Returns modules in order: dependencies first, root module last.
    /// </summary>
    private static List<ModuleDescriptor> TopologicalSort(
        Dictionary<Type, ModuleDescriptor> descriptors)
    {
        // Compute the in-degree of each node
        var inDegree = descriptors.ToDictionary(kv => kv.Key, _ => 0);
        var adjacency = descriptors.ToDictionary(kv => kv.Key, _ => new List<Type>());

        foreach ((Type type, ModuleDescriptor descriptor) in descriptors)
        {
            foreach (Type dep in descriptor.Dependencies)
            {
                // dep -> type: dep must be loaded before type
                adjacency[dep].Add(type);
                inDegree[type]++;
            }
        }

        // Queue of nodes with no incoming edges
        Queue<Type> queue = new(
            inDegree.Where(kv => kv.Value == 0).Select(kv => kv.Key));
        List<ModuleDescriptor> sorted = [];

        while (queue.Count > 0)
        {
            Type current = queue.Dequeue();
            sorted.Add(descriptors[current]);

            foreach (Type neighbor in adjacency[current])
            {
                inDegree[neighbor]--;
                if (inDegree[neighbor] == 0)
                {
                    queue.Enqueue(neighbor);
                }
            }
        }

        if (sorted.Count != descriptors.Count)
        {
            IEnumerable<string> cycleTypes = descriptors.Keys
                .Except(sorted.Select(d => d.ModuleType))
                .Select(t => t.Name);
            throw new InvalidOperationException(
                $"Circular dependency detected among modules: {string.Join(", ", cycleTypes)}.");
        }

        return sorted;
    }
}
