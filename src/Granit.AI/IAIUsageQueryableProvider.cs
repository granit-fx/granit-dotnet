namespace Granit.AI;

/// <summary>
/// Provides <see cref="IQueryable{T}"/> access to AI usage records for use by
/// <c>Granit.QueryEngine</c> query endpoints.
/// </summary>
/// <remarks>
/// This is <b>not</b> a repository — it exposes raw queryables for the query engine.
/// All filtering, sorting, and pagination logic lives in <see cref="Granit.QueryEngine"/>.
/// The default no-op implementation returns empty queryables; the
/// <c>Granit.AI.EntityFrameworkCore</c> package replaces it with DbContext-backed sources.
/// </remarks>
public interface IAIUsageQueryableProvider
{
    /// <summary>Returns a queryable source for <see cref="AIUsageRecord"/> entities.</summary>
    IQueryable<AIUsageRecord> GetUsageRecords();
}
