namespace Granit.Entities;

/// <summary>
/// Base class for declaring an entity's UI surface — the fluent C# entry point that
/// drives the <c>Granit.Entities</c> manifest. Mirrors the
/// <c>QueryDefinition&lt;TEntity&gt;</c> / <c>ExportDefinition&lt;TEntity&gt;</c> /
/// <c>MetricDefinition&lt;TEntity, TValue&gt;</c> patterns.
/// </summary>
/// <typeparam name="TEntity">The entity type the definition targets.</typeparam>
/// <remarks>
/// <para>
/// Each entity definition is registered as a singleton via
/// <c>services.AddEntityDefinition&lt;TEntity, TDefinition&gt;()</c>. The
/// <see cref="Configure"/> method is called exactly once per process at registration
/// time; the resulting descriptor is cached and exposed through
/// <see cref="IEntityDefinitionDescriptor"/>.
/// </para>
/// <para>
/// Example (Phase 1 cobaye):
/// <code>
/// public sealed class PartyEntityDefinition : EntityDefinition&lt;Party&gt;
/// {
///     public override string Name =&gt; "Granit.Parties.Party";
///
///     protected override void Configure(EntityDefinitionBuilder&lt;Party&gt; b)
///     {
///         b.DisplayKey("Entity:Party").Icon("users").PermissionGroup("Parties.Parties");
///         b.Query&lt;PartyQueryDefinition&gt;();
///         b.Export&lt;PartyExportDefinition&gt;();
///
///         b.Form("default", f =&gt; f
///             .Section("identity", s =&gt; s
///                 .Field(p =&gt; p.DisplayName)
///                 .Field(p =&gt; p.Status))
///             .Customizable());
///
///         b.Detail("default", d =&gt; d
///             .SectionsFromForm()
///             .SidePanel.Audit().Timeline());
///     }
/// }
/// </code>
/// </para>
/// </remarks>
public abstract class EntityDefinition<TEntity> : IEntityDefinitionDescriptor where TEntity : class
{
    private EntityDefinitionDescriptor? _descriptor;
    private readonly Lock _buildLock = new();

    /// <summary>Wire identifier (e.g. <c>"Granit.Parties.Party"</c>). MUST be unique across all loaded entity definitions.</summary>
    public abstract string Name { get; }

    /// <inheritdoc />
    public Type EntityType => typeof(TEntity);

    /// <inheritdoc />
    public EntityDefinitionDescriptor Descriptor
    {
        get
        {
            if (_descriptor is not null)
            {
                return _descriptor;
            }

            lock (_buildLock)
            {
                if (_descriptor is null)
                {
                    EntityDefinitionBuilder<TEntity> builder = new();
                    Configure(builder);
                    _descriptor = builder.Build(Name);
                }
            }

            return _descriptor;
        }
    }

    /// <summary>
    /// Configure the entity's UI surface using the fluent builder. Called exactly once
    /// per process; the resulting descriptor is immutable and cached.
    /// </summary>
    /// <param name="builder">The fluent entity-definition builder.</param>
    protected abstract void Configure(EntityDefinitionBuilder<TEntity> builder);
}
