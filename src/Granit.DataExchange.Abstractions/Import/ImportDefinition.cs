namespace Granit.DataExchange.Import;

/// <summary>
/// Base class for declaring how an entity type is imported.
/// Uses the Fluent API pattern — no attributes on the domain model.
/// </summary>
/// <typeparam name="TEntity">The target entity type.</typeparam>
/// <remarks>
/// <para>
/// Each import definition is registered as a singleton via
/// <c>services.AddImportDefinition&lt;TEntity, TDefinition&gt;()</c>.
/// The <see cref="Configure"/> method is called once at startup.
/// </para>
/// <para>
/// Example:
/// <code>
/// public sealed class PatientImportDefinition : ImportDefinition&lt;Patient&gt;
/// {
///     public override string Name =&gt; "Acme.PatientImport";
///     protected override void Configure(ImportDefinitionBuilder&lt;Patient&gt; builder)
///     {
///         builder
///             .HasBusinessKey(p =&gt; p.Niss)
///             .Property(p =&gt; p.Niss, p =&gt; p.DisplayName("NISS").Required())
///             .Property(p =&gt; p.Email, p =&gt; p.DisplayName("Email").Aliases("Courriel"));
///     }
/// }
/// </code>
/// </para>
/// </remarks>
public abstract class ImportDefinition<TEntity> : IImportDefinitionDescriptor where TEntity : class
{
    private ImportDefinitionBuilder<TEntity>? _builder;

    /// <summary>
    /// Unique name identifying this import definition (e.g. <c>"Acme.PatientImport"</c>).
    /// </summary>
    public abstract string Name { get; }

    /// <summary>
    /// The entity type this definition applies to.
    /// </summary>
    public Type EntityType => typeof(TEntity);

    /// <summary>
    /// Maximum allowed file size in megabytes. Default: <c>50</c>.
    /// </summary>
    public virtual int MaxFileSizeMb => 50;

    /// <summary>
    /// Allowed MIME types for upload. Default: CSV and XLSX.
    /// </summary>
    public virtual IReadOnlyList<string> AllowedMimeTypes =>
    [
        "text/csv",
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
    ];

    /// <summary>
    /// Configures the import definition using the fluent builder.
    /// Called once at startup.
    /// </summary>
    /// <param name="builder">The definition builder.</param>
    protected abstract void Configure(ImportDefinitionBuilder<TEntity> builder);

    /// <summary>
    /// Gets the built definition metadata (lazily initialized).
    /// </summary>
    internal ImportDefinitionBuilder<TEntity> GetBuilder()
    {
        if (_builder is not null)
        {
            return _builder;
        }

        _builder = new ImportDefinitionBuilder<TEntity>();
        Configure(_builder);
        return _builder;
    }

    /// <summary>
    /// Gets the declared importable properties.
    /// </summary>
    public IReadOnlyList<PropertyMapping> GetProperties() => GetBuilder().Properties.AsReadOnly();

    /// <summary>
    /// Gets the field metadata for all declared properties (for the mapping suggestion pipeline).
    /// </summary>
    public IReadOnlyList<ImportFieldMetadata> GetFieldMetadata() =>
        GetBuilder().Properties.Select(p => p.ToFieldMetadata()).ToList().AsReadOnly();

    /// <summary>
    /// Gets the business key property names.
    /// </summary>
    public IReadOnlyList<string> GetBusinessKeyProperties() => GetBuilder().BusinessKeyProperties.AsReadOnly();

    /// <summary>
    /// Gets properties excluded from UPDATE operations.
    /// </summary>
    public IReadOnlyList<string> GetExcludedOnUpdateProperties() => GetBuilder().ExcludedOnUpdateProperties.AsReadOnly();

    /// <summary>
    /// Gets the group-by column name, or <c>null</c> if no grouping is configured.
    /// </summary>
    public string? GetGroupByColumn() => GetBuilder().GroupByColumn;

    /// <summary>
    /// Gets whether External ID-based identity resolution is enabled.
    /// </summary>
    public bool GetHasExternalId() => GetBuilder().HasExternalIdFlag;
}
