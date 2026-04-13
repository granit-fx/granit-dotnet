namespace Granit.QueryEngine;

/// <summary>
/// Provides an <see cref="IQueryable{T}"/> source for query engine endpoints.
/// </summary>
/// <typeparam name="TEntity">The entity (or projected DTO) type exposed to the query engine.</typeparam>
/// <remarks>
/// <para>
/// Implement this interface in persistence layers (e.g. EF Core) to supply
/// <c>MapGranitQuery&lt;TEntity&gt;</c> with its base queryable. When registered in DI,
/// <c>MapGranitQuery</c> resolves it automatically without requiring an explicit
/// <c>sourceProvider</c> delegate.
/// </para>
/// <para>
/// This is <b>not</b> a repository — it exposes raw queryables for the query engine.
/// All filtering, sorting, and pagination logic lives in <see cref="IQueryEngine{TEntity}"/>.
/// </para>
/// </remarks>
public interface IQueryableSource<out TEntity> where TEntity : class
{
    /// <summary>Returns the base <see cref="IQueryable{T}"/> for <typeparamref name="TEntity"/>.</summary>
    IQueryable<TEntity> GetQueryable();
}
