using Granit.Authentication.JwtBearer;
using Granit.Authentication.JwtBearer.GoogleCloud.Extensions;
using Granit.Modularity;

namespace Granit.Authentication.JwtBearer.GoogleCloud;

/// <summary>
/// Granit module for Google Cloud Identity Platform (Firebase Auth) extras
/// (claims transformation, Admin policy).
/// Depends on <see cref="GranitJwtBearerModule"/> for generic JWT Bearer.
/// </summary>
[DependsOn(typeof(GranitJwtBearerModule))]
public sealed class GranitJwtBearerGoogleCloudModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.AddGranitGoogleCloudAuthentication();
}
