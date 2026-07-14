using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using Granit.DataExchange.Export.Pipeline;
using Granit.QueryEngine;

namespace Granit.DataExchange.Export.Internal;

/// <summary>
/// Default <see cref="IExportPipeline"/> implementation for a single entity type.
/// </summary>
/// <remarks>
/// <para>
/// Replaces the reflection bridges the orchestrator previously used
/// (<c>MakeGenericType</c> + <c>MethodInfo.Invoke</c> + boxed <c>IAsyncEnumerable&lt;object&gt;</c>)
/// and the per-row <c>PropertyInfo.GetValue</c> walks: property paths are compiled once into
/// <see cref="Func{TEntity, TResult}"/> delegates (with null-propagation for dotted paths) and
/// cached per closed generic type, and each row is an ordered <c>object?[]</c> aligned to the
/// context field list — no per-row dictionary allocation.
/// </para>
/// <para>
/// Compiled path getters bind against the <b>static</b> property types along the path (matching
/// the declared entity model), whereas the legacy reflection walk used runtime types. Fields are
/// declared against the entity's compile-time shape, so this is behavior-preserving for all
/// definitions built via <see cref="ExportDefinition{TEntity}"/> or reflection auto-definitions.
/// </para>
/// </remarks>
internal sealed class ExportPipeline<TEntity>(
    IExportDefinitionDescriptor definition,
    IExportDataSource<TEntity> dataSource,
    IQueryEngine<TEntity>? queryEngine,
    IExtraExportFieldProvider extraFieldProvider,
    IExportExtraValueResolver extraValueResolver) : IExportPipeline
    where TEntity : class
{
    /// <summary>
    /// Compiled property-path getters, cached per closed generic type. Keyed by the dotted
    /// property path; shared by every definition exporting the same entity type.
    /// </summary>
    private static readonly ConcurrentDictionary<string, Func<TEntity, object?>> PathGetterCache =
        new(StringComparer.Ordinal);

    /// <inheritdoc/>
    public string DefinitionName => definition.Name;

    /// <inheritdoc/>
    public Type EntityType => typeof(TEntity);

    /// <inheritdoc/>
    public IExportDefinitionDescriptor Definition => definition;

    /// <inheritdoc/>
    public async Task<long> WriteAsync(
        ExportPipelineContext context,
        Stream output,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(output);

        Func<TEntity, object?>[] getters = BuildGetters(context.Fields);
        IAsyncEnumerable<object?[]> rows = ProjectRowsAsync(context.Request, getters, cancellationToken);
        return await context.Writer.WriteAsync(output, context.Fields, rows, cancellationToken).ConfigureAwait(false);
    }

    private Func<TEntity, object?>[] BuildGetters(IReadOnlyList<ExportFieldDescriptor> fields)
    {
        HashSet<string>? extraPropertyNames = null;
        if (definition.IncludeMetadata)
        {
            IReadOnlyList<ExportFieldDescriptor> extraFields = extraFieldProvider.GetExtraFields(typeof(TEntity));
            if (extraFields.Count > 0)
            {
                extraPropertyNames = new HashSet<string>(
                    extraFields.Select(f => f.PropertyPath), StringComparer.Ordinal);
            }
        }

        var getters = new Func<TEntity, object?>[fields.Count];
        for (int i = 0; i < fields.Count; i++)
        {
            ExportFieldDescriptor field = fields[i];
            if (field.ValueSelector is not null)
            {
                Func<object, object?> selector = field.ValueSelector;
                getters[i] = entity => selector(entity);
            }
            else if (extraPropertyNames?.Contains(field.PropertyPath) == true)
            {
                string propertyName = field.PropertyPath;
                getters[i] = entity => extraValueResolver.ResolveExtraValue(entity, propertyName);
            }
            else
            {
                getters[i] = PathGetterCache.GetOrAdd(field.PropertyPath, CompilePathGetter);
            }
        }

        return getters;
    }

    private async IAsyncEnumerable<object?[]> ProjectRowsAsync(
        ExportRequest request,
        Func<TEntity, object?>[] getters,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (definition.QueryDefinitionName is not null)
        {
            IQueryEngine<TEntity> engine = queryEngine ?? throw new InvalidOperationException(
                $"Export definition '{definition.Name}' references query definition " +
                $"'{definition.QueryDefinitionName}' but no IQueryEngine<{typeof(TEntity).Name}> is available.");

            QueryRequest queryRequest = new()
            {
                Sort = request.Sort,
                Filter = request.Filter,
                Presets = request.Presets,
                Search = request.Search,
            };

            await foreach (TEntity entity in engine
                .ExecuteStreamAsync(dataSource.GetQueryable(), queryRequest, cancellationToken)
                .WithCancellation(cancellationToken)
                .ConfigureAwait(false))
            {
                yield return ProjectRow(entity, getters);
            }
        }
        else
        {
            // No query definition — enumerate the guarded queryable directly (sync iteration).
            foreach (TEntity entity in dataSource.GetQueryable())
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return ProjectRow(entity, getters);
            }
        }
    }

    private static object?[] ProjectRow(TEntity entity, Func<TEntity, object?>[] getters)
    {
        object?[] row = new object?[getters.Length];
        for (int i = 0; i < getters.Length; i++)
        {
            row[i] = getters[i](entity);
        }

        return row;
    }

    /// <summary>
    /// Compiles a dotted property path into a null-propagating getter, equivalent to
    /// <c>e => e.A == null ? null : (object?)e.A.B</c>. A path segment that does not exist on
    /// the static type yields a constant-null getter (same behavior as the legacy reflection walk).
    /// </summary>
    private static Func<TEntity, object?> CompilePathGetter(string propertyPath)
    {
        string[] segments = propertyPath.Split('.');
        ParameterExpression entity = Expression.Parameter(typeof(TEntity), "entity");
        LabelTarget exit = Expression.Label(typeof(object), "exit");

        List<ParameterExpression> locals = [];
        List<Expression> body = [];
        Expression current = entity;

        for (int i = 0; i < segments.Length; i++)
        {
            PropertyInfo? property = current.Type.GetProperty(segments[i]);
            if (property is null)
            {
                return static _ => null;
            }

            ParameterExpression local = Expression.Variable(property.PropertyType, $"v{i}");
            locals.Add(local);
            body.Add(Expression.Assign(local, Expression.Property(current, property)));

            bool canBeNull = !property.PropertyType.IsValueType
                || Nullable.GetUnderlyingType(property.PropertyType) is not null;
            if (i < segments.Length - 1 && canBeNull)
            {
                body.Add(Expression.IfThen(
                    Expression.Equal(local, Expression.Constant(null, property.PropertyType)),
                    Expression.Return(exit, Expression.Constant(null, typeof(object)))));
            }

            current = local;
        }

        body.Add(Expression.Label(exit, Expression.Convert(current, typeof(object))));
        BlockExpression block = Expression.Block(typeof(object), locals, body);
        return Expression.Lambda<Func<TEntity, object?>>(block, entity).Compile();
    }
}
