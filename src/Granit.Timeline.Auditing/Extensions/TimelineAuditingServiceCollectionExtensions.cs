using Granit.Timeline.Abstractions;
using Granit.Timeline.Auditing.Internal;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.Timeline.Auditing.Extensions;

/// <summary>
/// Extension methods for plugging the Auditing bridge into Granit.Timeline.
/// </summary>
public static class TimelineAuditingServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="AuditingTimelineSource"/> as an
    /// <see cref="ITimelineSource"/>. Once registered, every <c>AuditEntry</c>
    /// targeting an <c>ITimelined</c> entity appears in that entity's
    /// federated timeline (audit retention applies — entries roll off when
    /// the audit log purges them, unless anchored by a reaction/reply).
    /// </summary>
    public static IServiceCollection AddGranitTimelineAuditing(this IServiceCollection services)
    {
        services.AddScoped<ITimelineSource, AuditingTimelineSource>();
        return services;
    }
}
