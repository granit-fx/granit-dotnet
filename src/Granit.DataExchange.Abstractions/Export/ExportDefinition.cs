namespace Granit.DataExchange.Export;

/// <summary>
/// Base class for declaring how an entity type is exported.
/// Uses the Fluent API pattern — no attributes on the domain model.
/// </summary>
/// <typeparam name="TEntity">The source entity type.</typeparam>
/// <remarks>
/// <para>
/// Each export definition is registered as a singleton via
/// <c>services.AddExportDefinition&lt;TEntity, TDefinition&gt;()</c>.
/// The <see cref="Configure"/> method is called once at startup.
/// </para>
/// <para>
/// Only fields explicitly declared in <see cref="Configure"/> are available
/// for export (whitelist approach, inspired by Django's <c>ExportResource</c>).
/// </para>
/// <para>
/// When <see cref="QueryDefinitionName"/> is set, the export pipeline delegates
/// filtering and sorting to <c>IQueryEngine&lt;TEntity&gt;</c> from Granit.QueryEngine,
/// reusing the same pipeline as the grid view.
/// </para>
/// <para>
/// Example:
/// <code>
/// public sealed class PatientExportDefinition : ExportDefinition&lt;Patient&gt;
/// {
///     public override string Name =&gt; "Acme.PatientExport";
///     public override string? QueryDefinitionName =&gt; "Acme.Patients";
///     protected override void Configure(ExportDefinitionBuilder&lt;Patient&gt; builder)
///     {
///         builder
///             .IncludeBusinessKey()
///             .Field(p =&gt; p.LastName, f =&gt; f.Header("Nom"))
///             .Field(p =&gt; p.Email)
///             .Field(p =&gt; p.Company, c =&gt; c.Name, f =&gt; f.Header("Société"));
///     }
/// }
/// </code>
/// </para>
/// </remarks>
public abstract class ExportDefinition<TEntity> : IExportDefinitionDescriptor
    where TEntity : class
{
    private ExportDefinitionBuilder<TEntity>? _builder;

    /// <summary>
    /// Unique name identifying this export definition (e.g. <c>"Acme.PatientExport"</c>).
    /// </summary>
    public abstract string Name { get; }

    /// <inheritdoc/>
    public Type EntityType => typeof(TEntity);

    /// <summary>
    /// Name of the associated <c>QueryDefinition</c> for filtering and sorting.
    /// When set, the export pipeline uses <c>IQueryEngine</c> to apply the same
    /// filtering/sorting pipeline as the grid view.
    /// When <c>null</c>, no filtering or sorting is applied (export all data).
    /// </summary>
    public virtual string? QueryDefinitionName => null;

    /// <summary>
    /// Supported output formats. Default: <c>["xlsx", "csv"]</c>.
    /// </summary>
    public virtual IReadOnlyList<string> SupportedFormats => ["xlsx", "csv"];

    /// <summary>
    /// Configures the export definition using the fluent builder.
    /// Called once at startup.
    /// </summary>
    /// <param name="builder">The definition builder.</param>
    protected abstract void Configure(ExportDefinitionBuilder<TEntity> builder);

    /// <summary>
    /// Gets the built definition metadata (lazily initialized).
    /// </summary>
    internal ExportDefinitionBuilder<TEntity> GetBuilder()
    {
        if (_builder is not null)
        {
            return _builder;
        }

        _builder = new ExportDefinitionBuilder<TEntity>();
        Configure(_builder);
        return _builder;
    }

    /// <inheritdoc/>
    public IReadOnlyList<ExportFieldDescriptor> GetFields() =>
        GetBuilder().Fields.AsReadOnly();

    /// <summary>
    /// Gets whether the entity <c>Id</c> should be included for import-compatible export.
    /// </summary>
    public bool GetIncludeId() => GetBuilder().IncludeIdFlag;

    /// <summary>
    /// Gets whether business key columns should be included for roundtrip import.
    /// </summary>
    public bool GetIncludeBusinessKey() => GetBuilder().IncludeBusinessKeyFlag;

    /// <summary>
    /// Gets whether mapped extra properties should be appended to the export fields.
    /// </summary>
    public bool GetIncludeExtraProperties() => GetBuilder().IncludeExtraPropertiesFlag;

    /// <inheritdoc/>
    bool IExportDefinitionDescriptor.IncludeExtraProperties => GetIncludeExtraProperties();
}
