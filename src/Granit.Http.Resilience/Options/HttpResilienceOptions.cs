namespace Granit.Http.Resilience.Options;

/// <summary>
/// Global HTTP resilience configuration.
/// Per-client pipeline overrides are read from <c>HttpResilience:{clientName}:*</c>
/// in <c>appsettings.json</c> and applied on top of the
/// <c>AddStandardResilienceHandler</c> defaults.
/// </summary>
/// <remarks>
/// Default pipeline (from <c>AddStandardResilienceHandler</c>):
/// <list type="bullet">
///   <item><description>Total timeout: 30 s</description></item>
///   <item><description>Retry: 3 attempts, exponential back-off (2 s base)</description></item>
///   <item><description>Circuit breaker: opens after 10 failures in 30 s, stays open for 1 min</description></item>
///   <item><description>Per-attempt timeout: 10 s</description></item>
/// </list>
/// </remarks>
public sealed class HttpResilienceOptions
{
    /// <summary>The root appsettings section under which per-client overrides are nested.</summary>
    public const string SectionName = "HttpResilience";
}
