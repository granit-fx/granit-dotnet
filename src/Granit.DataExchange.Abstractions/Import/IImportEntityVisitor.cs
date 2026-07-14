namespace Granit.DataExchange.Import;

/// <summary>
/// Visitor over the entity type of an <see cref="IImportEntityBinding"/>.
/// Bridges the generic gap between runtime definition discovery and
/// statically-typed pipeline construction — no <c>MakeGenericMethod</c> involved.
/// </summary>
/// <typeparam name="TResult">The result type produced per visited definition.</typeparam>
public interface IImportEntityVisitor<out TResult>
{
    /// <summary>
    /// Visits a definition with its entity type statically known.
    /// </summary>
    /// <typeparam name="TEntity">The target entity type of the definition.</typeparam>
    /// <param name="definition">The import definition.</param>
    /// <returns>The visitor's result.</returns>
    TResult Visit<TEntity>(ImportDefinition<TEntity> definition) where TEntity : class;
}
