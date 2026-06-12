using Granit.AI;
using Granit.Diagnostics;
using Granit.Modularity;
using Granit.UserSessions.AnomalyDetection.Diagnostics;
using Granit.UserSessions.AnomalyDetection.Internal;
using Granit.UserSessions.AnomalyDetection.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.UserSessions.AnomalyDetection;

/// <summary>
/// Granit module for opt-in session anomaly detection. Replaces the no-op
/// <see cref="IUserSessionAnomalyDetector"/> default from <c>Granit.UserSessions.Abstractions</c> with the
/// heuristic (and optionally AI-assisted) detector, and registers <see cref="IUserSessionRiskEvaluator"/>.
/// </summary>
[DependsOn(typeof(GranitAIModule), typeof(GranitUserSessionsAbstractionsModule))]
public sealed class GranitUserSessionsAnomalyDetectionModule : GranitModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        GranitActivitySourceRegistry.Register(UserSessionsAnomalyDetectionActivitySource.Name);

        context.Services
            .AddOptions<UserSessionsAnomalyDetectionOptions>()
            .BindConfiguration(UserSessionsAnomalyDetectionOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        context.Services.TryAddSingleton<UserSessionsAnomalyDetectionMetrics>();
        context.Services.AddScoped<IUserSessionAnomalyDetector, UserSessionAnomalyDetector>();
        context.Services.AddScoped<IUserSessionRiskEvaluator, DefaultUserSessionRiskEvaluator>();
    }
}
