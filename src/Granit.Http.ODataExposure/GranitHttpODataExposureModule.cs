using Granit.Authorization;
using Granit.Http.ODataExposure.Extensions;
using Granit.Modularity;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.Http.ODataExposure;

/// <summary>
/// Granit module for the OData v4 exposure layer. Once loaded, the host can
/// call <c>endpoints.MapGranitODataEndpoints(prefix, opts =&gt; opts.EntitySet&lt;...&gt;)</c>
/// to expose its registered <c>QueryDefinition&lt;T&gt;</c> instances as
/// queryable EntitySets for Power BI, Excel, Tableau and Qlik.
/// </summary>
[DependsOn(typeof(GranitAuthorizationModule))]
public sealed class GranitHttpODataExposureModule : GranitModule
{
    /// <inheritdoc />
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.AddGranitODataExposure();
}
