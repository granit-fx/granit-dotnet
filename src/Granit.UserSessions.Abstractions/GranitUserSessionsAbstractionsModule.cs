using Granit.Modularity;
using Granit.UserSessions.Internal;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.UserSessions;

/// <summary>
/// Granit module for shared session contracts (<see cref="UserSessionDescriptor"/>,
/// <see cref="IUserSessionAnomalyDetector"/>, <see cref="IUserSessionRiskStore"/>).
/// </summary>
/// <remarks>
/// Registers safe no-op / in-memory defaults so the contracts resolve everywhere. Install
/// <c>Granit.UserSessions.AnomalyDetection</c> to replace the detector and
/// <c>Granit.UserSessions.EntityFrameworkCore</c> to replace the risk store with a durable one.
/// </remarks>
public sealed class GranitUserSessionsAbstractionsModule : GranitModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.TryAddScoped<IUserSessionAnomalyDetector, NullUserSessionAnomalyDetector>();
        context.Services.TryAddSingleton<IUserSessionRiskStore, MemoryUserSessionRiskStore>();
    }
}
