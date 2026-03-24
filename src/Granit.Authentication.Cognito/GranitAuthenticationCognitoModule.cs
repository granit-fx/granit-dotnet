using Granit.Authentication.Cognito.Extensions;
using Granit.Authentication.JwtBearer;
using Granit.Modularity;

namespace Granit.Authentication.Cognito;

/// <summary>
/// Granit module for AWS Cognito extras (claims transformation, Admin policy).
/// Depends on <see cref="GranitJwtBearerModule"/> for generic JWT Bearer.
/// </summary>
[DependsOn(typeof(GranitJwtBearerModule))]
public sealed class GranitAuthenticationCognitoModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.AddGranitCognito();
}
