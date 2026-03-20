namespace Granit.AI.Internal;

/// <summary>
/// Default no-op implementation of <see cref="IAIUsageQueryableProvider"/>.
/// Returns empty queryables. Replaced by <c>EfAIUsageQueryableProvider</c> when
/// <c>Granit.AI.EntityFrameworkCore</c> is loaded.
/// </summary>
internal sealed class NullAIUsageQueryableProvider : IAIUsageQueryableProvider
{
    public IQueryable<AIUsageRecord> GetUsageRecords() =>
        Enumerable.Empty<AIUsageRecord>().AsQueryable();
}
