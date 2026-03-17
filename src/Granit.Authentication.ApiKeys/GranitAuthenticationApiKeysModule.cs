using Granit.Authentication.ApiKeys.Extensions;
using Granit.Core.Modularity;
using Granit.Guids;
using Granit.Http.ExceptionHandling;
using Granit.Querying;
using Granit.Security;
using Granit.Timing;

namespace Granit.Authentication.ApiKeys;

/// <summary>
/// Granit module that registers API key authentication services.
/// </summary>
[DependsOn(typeof(GranitExceptionHandlingModule))]
[DependsOn(typeof(GranitGuidsModule))]
[DependsOn(typeof(GranitQueryingModule))]
[DependsOn(typeof(GranitSecurityModule))]
[DependsOn(typeof(GranitTimingModule))]
public sealed class GranitAuthenticationApiKeysModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.AddGranitApiKeyAuthentication();
}
