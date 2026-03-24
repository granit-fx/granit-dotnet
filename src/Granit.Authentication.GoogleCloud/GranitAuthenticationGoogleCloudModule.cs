using Granit.Authentication.GoogleCloud.Extensions;
using Granit.Authentication.JwtBearer;
using Granit.Modularity;

namespace Granit.Authentication.GoogleCloud;

/// <summary>
/// Granit module for Google Cloud Identity Platform (Firebase Auth) extras
/// (claims transformation, Admin policy).
/// Depends on <see cref="GranitJwtBearerModule"/> for generic JWT Bearer.
/// </summary>
[DependsOn(typeof(GranitJwtBearerModule))]
public sealed class GranitAuthenticationGoogleCloudModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.AddGranitGoogleCloudAuthentication();
}
