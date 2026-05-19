using Granit.Authentication.JwtBearer.GoogleCloud.Extensions;
using Granit.Modularity;

namespace Granit.Authentication.JwtBearer.GoogleCloud;

/// <summary>
/// Granit module for Google Cloud Identity Platform (Firebase Auth) extras
/// (claims transformation, Admin policy).
/// Depends on <see cref="GranitAuthenticationJwtBearerModule"/> for generic JWT Bearer.
/// </summary>
[DependsOn(typeof(GranitAuthenticationJwtBearerModule))]
public sealed class GranitAuthenticationJwtBearerGoogleCloudModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.AddGranitGoogleCloudAuthentication();
}
