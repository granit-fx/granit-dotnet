using Granit.Modularity;

namespace Granit.Http.Resilience;

/// <summary>
/// Granit module for standardized outbound HTTP resilience.
/// </summary>
/// <remarks>
/// Exposes <c>AddGranitHttpClient()</c> which applies a standard resilience pipeline
/// (retry with exponential back-off, circuit breaker, per-request timeout) to named
/// <see cref="System.Net.Http.HttpClient"/> instances via
/// <c>Microsoft.Extensions.Http.Resilience</c>.
/// Per-client settings are overridable from <c>HttpResilience:{clientName}:*</c> in
/// <c>appsettings.json</c>. Runtime telemetry is emitted by the upstream <c>Polly</c>
/// meter shipped with <c>Microsoft.Extensions.Http.Resilience</c>.
/// </remarks>
public sealed class GranitHttpResilienceModule : GranitModule;
