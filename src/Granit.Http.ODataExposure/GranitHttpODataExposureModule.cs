using Granit.Authorization;
using Granit.Http.ODataExposure.Extensions;
using Granit.Http.RateLimiting;
using Granit.Modularity;

namespace Granit.Http.ODataExposure;

/// <summary>
/// Granit module for the OData v4 exposure layer. Once loaded, the host can
/// call <c>endpoints.MapGranitODataEndpoints(prefix, opts =&gt; opts.EntitySet&lt;...&gt;)</c>
/// to expose its registered <c>QueryDefinition&lt;T&gt;</c> instances as
/// queryable EntitySets for Power BI, Excel, Tableau and Qlik.
/// </summary>
/// <remarks>
/// Depends on <see cref="GranitHttpRateLimitingModule"/> because every OData
/// route is gated by the <c>"granit-odata"</c> rate-limit policy (C3b
/// hardening). The host MUST configure the policy in
/// <c>appsettings.json</c> under <c>RateLimiting:Policies:granit-odata</c>;
/// see <c>Granit.Http.ODataExposure</c> README for the recommended defaults.
/// </remarks>
[DependsOn(typeof(GranitAuthorizationModule), typeof(GranitHttpRateLimitingModule))]
public sealed class GranitHttpODataExposureModule : GranitModule
{
    /// <inheritdoc />
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.AddGranitODataExposure();
}
