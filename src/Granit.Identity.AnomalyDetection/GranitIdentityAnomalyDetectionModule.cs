using Granit.AI;
using Granit.Diagnostics;
using Granit.Identity.AnomalyDetection.Diagnostics;
using Granit.Identity.AnomalyDetection.Internal;
using Granit.Identity.AnomalyDetection.Options;
using Granit.Modularity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.Identity.AnomalyDetection;

/// <summary>
/// Granit module for opt-in session anomaly detection. Replaces the no-op
/// <see cref="IUserSessionAnomalyDetector"/> default from <c>Granit.Identity.Abstractions</c> with the
/// heuristic (and optionally AI-assisted) detector, and registers <see cref="IUserSessionRiskEvaluator"/>.
/// </summary>
[DependsOn(typeof(GranitAIModule), typeof(GranitIdentityAbstractionsModule))]
public sealed class GranitIdentityAnomalyDetectionModule : GranitModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        GranitActivitySourceRegistry.Register(IdentityAnomalyDetectionActivitySource.Name);

        context.Services
            .AddOptions<IdentityAnomalyDetectionOptions>()
            .BindConfiguration(IdentityAnomalyDetectionOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        context.Services.TryAddSingleton<IdentityAnomalyDetectionMetrics>();
        context.Services.AddScoped<IUserSessionAnomalyDetector, UserSessionAnomalyDetector>();
        context.Services.AddScoped<IUserSessionRiskEvaluator, DefaultUserSessionRiskEvaluator>();
    }
}
