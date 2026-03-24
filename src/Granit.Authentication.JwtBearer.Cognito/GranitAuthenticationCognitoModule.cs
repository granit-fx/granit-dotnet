using Granit.Authentication.JwtBearer;
using Granit.Authentication.JwtBearer.Cognito.Extensions;
using Granit.Modularity;

namespace Granit.Authentication.JwtBearer.Cognito;

/// <summary>
/// Granit module for AWS Cognito extras (claims transformation, Admin policy).
/// Depends on <see cref="GranitJwtBearerModule"/> for generic JWT Bearer.
/// </summary>
[DependsOn(typeof(GranitJwtBearerModule))]
public sealed class GranitJwtBearerCognitoModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.AddGranitCognito();
}
