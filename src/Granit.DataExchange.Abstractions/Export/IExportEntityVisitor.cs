namespace Granit.DataExchange.Export;

/// <summary>
/// Generic visitor over the entity type captured by an <see cref="IExportEntityBinding"/>.
/// </summary>
/// <typeparam name="TResult">The result type produced by the visit.</typeparam>
/// <remarks>
/// Implementations receive the strongly typed <see cref="ExportDefinition{TEntity}"/> and can
/// build fully typed infrastructure (e.g. an export pipeline bound to <c>TEntity</c>) without
/// any <c>MakeGenericType</c>/<c>MakeGenericMethod</c> reflection at execution time.
/// </remarks>
public interface IExportEntityVisitor<out TResult>
{
    /// <summary>
    /// Visits the export definition with its concrete entity type.
    /// </summary>
    /// <typeparam name="TEntity">The source entity type of the definition.</typeparam>
    /// <param name="definition">The strongly typed export definition.</param>
    /// <returns>The visitor result.</returns>
    TResult Visit<TEntity>(ExportDefinition<TEntity> definition)
        where TEntity : class;
}
