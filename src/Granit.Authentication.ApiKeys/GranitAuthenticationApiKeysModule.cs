using Granit.Authentication.ApiKeys.Extensions;
using Granit.Guids;
using Granit.Http.ExceptionHandling;
using Granit.Modularity;
using Granit.Querying;
using Granit.Timing;
using Granit.Users;

namespace Granit.Authentication.ApiKeys;

/// <summary>
/// Granit module that registers API key authentication services.
/// </summary>
[DependsOn(typeof(GranitExceptionHandlingModule))]
[DependsOn(typeof(GranitGuidsModule))]
[DependsOn(typeof(GranitQueryingModule))]
[DependsOn(typeof(GranitTimingModule))]
public sealed class GranitAuthenticationApiKeysModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.AddGranitApiKeyAuthentication();
}
