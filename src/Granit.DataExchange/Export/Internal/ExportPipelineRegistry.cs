using System.Reflection;
using Granit.DataExchange.Export.Pipeline;

namespace Granit.DataExchange.Export.Internal;

/// <summary>
/// Default <see cref="IExportPipelineRegistry"/> implementation.
/// </summary>
/// <remarks>
/// <para>
/// Composed from two sources at (lazy, thread-safe) initialization:
/// </para>
/// <list type="number">
///   <item>Explicit <see cref="IExportEntityBinding"/> singletons — turned into fully typed
///   descriptors through the generic visitor, with zero reflection.</item>
///   <item><see cref="IAutoExportDefinitionSource"/> entity types — one
///   <c>MakeGenericMethod</c> call per entity type, executed once at initialization; the
///   resulting descriptor caches the pipeline factory for the process lifetime.</item>
/// </list>
/// <para>
/// Explicit definitions win: auto definitions are skipped for entity types that already have an
/// explicit binding, and on a (pathological) name collision the first registration wins —
/// preserving the resolution semantics of the legacy <c>ExportDefinitionProvider</c>.
/// Name lookups are ordinal (case-sensitive).
/// </para>
/// </remarks>
internal sealed class ExportPipelineRegistry : IExportPipelineRegistry
{
    private static readonly MethodInfo CreateTypedDescriptorMethod = typeof(ExportPipelineRegistry)
        .GetMethod(nameof(CreateTypedDescriptor), BindingFlags.Public | BindingFlags.Static)!;

    private readonly Lazy<Snapshot> _snapshot;

    public ExportPipelineRegistry(
        IEnumerable<IExportEntityBinding> bindings,
        IEnumerable<IAutoExportDefinitionSource> autoSources,
        IExtraExportFieldProvider extraFieldProvider)
    {
        ArgumentNullException.ThrowIfNull(bindings);
        ArgumentNullException.ThrowIfNull(autoSources);
        ArgumentNullException.ThrowIfNull(extraFieldProvider);

        _snapshot = new Lazy<Snapshot>(
            () => Build(bindings, autoSources, extraFieldProvider),
            LazyThreadSafetyMode.ExecutionAndPublication);
    }

    /// <inheritdoc/>
    public IReadOnlyList<IExportPipelineDescriptor> GetAll() => _snapshot.Value.All;

    /// <inheritdoc/>
    public IExportPipelineDescriptor? Find(string definitionName)
    {
        ArgumentException.ThrowIfNullOrEmpty(definitionName);
        return _snapshot.Value.ByName.GetValueOrDefault(definitionName);
    }

    /// <summary>
    /// Closes <see cref="ExportPipelineDescriptor{TEntity}"/> over an auto-discovered entity
    /// type. Public on an internal class so the registry can reflect on it without
    /// <c>BindingFlags.NonPublic</c> (S3011).
    /// </summary>
    public static IExportPipelineDescriptor CreateTypedDescriptor<TEntity>(IExportDefinitionDescriptor definition)
        where TEntity : class =>
        new ExportPipelineDescriptor<TEntity>(definition);

    private static Snapshot Build(
        IEnumerable<IExportEntityBinding> bindings,
        IEnumerable<IAutoExportDefinitionSource> autoSources,
        IExtraExportFieldProvider extraFieldProvider)
    {
        List<IExportPipelineDescriptor> all = [];
        Dictionary<string, IExportPipelineDescriptor> byName = new(StringComparer.Ordinal);
        HashSet<Type> explicitEntityTypes = [];

        DescriptorFactoryVisitor visitor = new();
        foreach (IExportEntityBinding binding in bindings)
        {
            IExportPipelineDescriptor descriptor = binding.Accept(visitor);
            explicitEntityTypes.Add(descriptor.EntityType);
            if (byName.TryAdd(descriptor.DefinitionName, descriptor))
            {
                all.Add(descriptor);
            }
        }

        foreach (IAutoExportDefinitionSource source in autoSources)
        {
            foreach (Type entityType in source.GetEntityTypes())
            {
                if (explicitEntityTypes.Contains(entityType))
                {
                    continue;
                }

                var definition = new ReflectionExportDefinition(entityType, extraFieldProvider);
                var descriptor = (IExportPipelineDescriptor)CreateTypedDescriptorMethod
                    .MakeGenericMethod(entityType)
                    .Invoke(null, [definition])!;

                if (byName.TryAdd(descriptor.DefinitionName, descriptor))
                {
                    all.Add(descriptor);
                }
            }
        }

        return new Snapshot(all.AsReadOnly(), byName);
    }

    private sealed record Snapshot(
        IReadOnlyList<IExportPipelineDescriptor> All,
        Dictionary<string, IExportPipelineDescriptor> ByName);

    private sealed class DescriptorFactoryVisitor : IExportEntityVisitor<IExportPipelineDescriptor>
    {
        public IExportPipelineDescriptor Visit<TEntity>(ExportDefinition<TEntity> definition)
            where TEntity : class =>
            new ExportPipelineDescriptor<TEntity>(definition);
    }
}
