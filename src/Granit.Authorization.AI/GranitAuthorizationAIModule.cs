using Granit.AI;
using Granit.Authorization;
using Granit.Modularity;

namespace Granit.Authorization.AI;

/// <summary>
/// Granit module for AI-powered access anomaly detection.
/// </summary>
/// <remarks>
/// Registers <see cref="IAIAccessAnomalyDetector"/> as a scoped service backed by an LLM.
/// Authorization checks can inject the detector to evaluate access patterns for suspicious
/// behavior such as unusual permission requests, off-hours access, or geographic anomalies.
/// The service uses a fail-open design: when the LLM is unavailable, access is allowed
/// with a warning log for manual review.
/// </remarks>
[DependsOn(typeof(GranitAIModule))]
[DependsOn(typeof(GranitAuthorizationModule))]
public sealed class GranitAuthorizationAIModule : GranitModule;
