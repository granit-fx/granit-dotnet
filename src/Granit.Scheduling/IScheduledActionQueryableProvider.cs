using Granit.Scheduling.Domain;

namespace Granit.Scheduling;

/// <summary>
/// Provides an <see cref="IQueryable{T}"/> source for <see cref="ScheduledAction"/>
/// entities, enabling <c>MapGranitQuery</c> pagination, filtering, and sorting.
/// </summary>
public interface IScheduledActionQueryableProvider
{
    /// <summary>Returns a queryable source for <see cref="ScheduledAction"/> entities.</summary>
    IQueryable<ScheduledAction> GetScheduledActions();
}
