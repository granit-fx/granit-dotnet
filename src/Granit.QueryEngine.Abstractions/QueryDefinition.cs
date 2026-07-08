using System.Linq.Expressions;
using Granit.QueryEngine.Options;
namespace Granit.QueryEngine;

/// <summary>
/// Base class for declaring how an entity type is queried (filtered, sorted, paginated, grouped).
/// Uses the Fluent API pattern — no attributes on the domain model.
/// </summary>
/// <typeparam name="TEntity">The target entity type.</typeparam>
/// <remarks>
/// <para>
/// Each query definition is registered as a singleton via
/// <c>services.AddQueryDefinition&lt;TEntity, TDefinition&gt;()</c>.
/// The <see cref="Configure"/> method is called once at startup.
/// </para>
/// <para>
/// Example:
/// <code>
/// public sealed class PatientQueryDefinition : QueryDefinition&lt;Patient&gt;
/// {
///     public override string Name =&gt; "Acme.Patients";
///     protected override void Configure(QueryDefinitionBuilder&lt;Patient&gt; builder)
///     {
///         builder
///             .Column(p =&gt; p.Niss, c =&gt; c.Label("NISS").Filterable().Sortable())
///             .Column(p =&gt; p.Email, c =&gt; c.Label("Email").Filterable())
///             .GlobalSearch(p =&gt; p.Niss, p =&gt; p.Email)
///             .DefaultSort("-createdAt")
///             .DefaultPageSize(25);
///     }
/// }
/// </code>
/// </para>
/// </remarks>
public abstract class QueryDefinition<TEntity> : IQueryDefinitionDescriptor where TEntity : class
{
    private QueryDefinitionBuilder<TEntity>? _builder;
    private QueryEngineOptions _options = new();

    /// <summary>
    /// Unique name identifying this query definition (e.g. <c>"Acme.Patients"</c>).
    /// </summary>
    public abstract string Name { get; }

    /// <summary>
    /// The entity type this definition applies to.
    /// </summary>
    public Type EntityType => typeof(TEntity);

    /// <summary>
    /// Owning module of this query, used to group and sort the query catalogue. Defaults to the
    /// target entity's owning assembly with the framework <c>Granit.</c> prefix stripped (e.g.
    /// <c>"Auditing"</c>). Override only when the entity does not live in its module's assembly.
    /// </summary>
    public virtual string ModuleName => IQueryDefinitionDescriptor.ModuleOf(typeof(TEntity));

    /// <summary>
    /// The localization resource type used to resolve <see cref="ColumnDescriptor.LabelKey"/> values.
    /// Override in derived classes to point to the module's localization resource marker class.
    /// When <c>null</c>, no localization is attempted and <see cref="ColumnDescriptor.Label"/>
    /// (or the property name) is used as-is.
    /// </summary>
    public virtual Type? LocalizationResourceType => null;

    /// <summary>
    /// Optional permission the caller must hold to execute this query over HTTP and to see it in
    /// the query catalogue. Three-segment format <c>[Group].[Resource].[Action]</c> — must resolve
    /// to a declared <c>PermissionDefinition</c>. When <c>null</c> (the default), the query is
    /// gated by authentication only. Override to require a permission (least privilege, ISO 27001
    /// A.9.4).
    /// </summary>
    public virtual string? RequiredPermission => null;

    /// <summary>
    /// Configures the query definition using the fluent builder.
    /// Called once at startup.
    /// </summary>
    /// <param name="builder">The definition builder.</param>
    protected abstract void Configure(QueryDefinitionBuilder<TEntity> builder);

    /// <summary>
    /// Sets the global QueryEngine options. Called by the DI factory in
    /// <c>ServiceCollectionExtensions.AddQueryDefinition{TEntity, TDefinition}</c>
    /// before the builder is initialized.
    /// </summary>
    /// <param name="options">The global QueryEngine options.</param>
    internal void Initialize(QueryEngineOptions options) =>
        _options = options;

    /// <summary>
    /// Gets the built definition metadata (lazily initialized).
    /// </summary>
    internal QueryDefinitionBuilder<TEntity> GetBuilder()
    {
        if (_builder is not null)
        {
            return _builder;
        }

        _builder = new QueryDefinitionBuilder<TEntity>(_options);
        Configure(_builder);
        return _builder;
    }

    /// <summary>
    /// Gets the declared column descriptors.
    /// </summary>
    public IReadOnlyList<ColumnDescriptor> GetColumns() =>
        GetBuilder().Columns.AsReadOnly();

    /// <summary>
    /// Gets the declared filter groups.
    /// </summary>
    public IReadOnlyList<Filtering.FilterGroupDescriptor> GetFilterGroups() =>
        GetBuilder().FilterGroups.AsReadOnly();

    /// <summary>
    /// Gets the declared date filters.
    /// </summary>
    public IReadOnlyList<Filtering.DateFilterDescriptor> GetDateFilters() =>
        GetBuilder().DateFilters.AsReadOnly();

    /// <summary>
    /// Gets the declared group-by fields.
    /// </summary>
    public IReadOnlyList<Filtering.GroupByDescriptor> GetGroupByFields() =>
        GetBuilder().GroupByFields.AsReadOnly();

    /// <summary>
    /// Gets the declared aggregates.
    /// </summary>
    public IReadOnlyList<Filtering.AggregateDescriptor> GetAggregates() =>
        GetBuilder().Aggregates.AsReadOnly();

    /// <summary>
    /// Gets the declared quick filters.
    /// </summary>
    public IReadOnlyList<Filtering.QuickFilterDescriptor> GetQuickFilters() =>
        GetBuilder().QuickFilters.AsReadOnly();

    /// <summary>
    /// Gets the global search property names.
    /// </summary>
    public IReadOnlyList<string> GetGlobalSearchProperties() =>
        GetBuilder().GlobalSearchProperties.AsReadOnly();

    /// <summary>
    /// Gets the default page size.
    /// </summary>
    public int GetDefaultPageSize() =>
        GetBuilder().DefaultPageSizeValue;

    /// <summary>
    /// Gets the maximum allowed page size.
    /// </summary>
    public int GetMaxPageSize() =>
        GetBuilder().MaxPageSizeValue;

    /// <summary>
    /// Gets the cursor property name, or <c>null</c> if cursor pagination is not enabled.
    /// </summary>
    public string? GetCursorProperty() =>
        GetBuilder().CursorPropertyName;

    /// <summary>
    /// Gets the default sort specification, or <c>null</c>.
    /// </summary>
    public string? GetDefaultSort() =>
        GetBuilder().DefaultSortValue;

    /// <summary>
    /// Gets the declared projection DTO type, or <c>null</c> when no projection is configured.
    /// </summary>
    public Type? GetProjectionType() =>
        GetBuilder().ProjectionType;

    /// <summary>
    /// Gets the declared projection expression, or <c>null</c> when no projection is configured.
    /// </summary>
    public LambdaExpression? GetProjectionExpression() =>
        GetBuilder().ProjectionExpression;

    /// <summary>
    /// Gets the lookup-source declaration (via
    /// <see cref="QueryDefinitionBuilder{TEntity}.AsLookup{TValue}"/>), or <c>null</c> when
    /// this definition is not exposed as a data-lookup source.
    /// </summary>
    public LookupSourceDescriptor? GetLookupSource() =>
        GetBuilder().LookupSourceValue;
}
