using Granit.Core.Modularity;
using Granit.Identity.Federated.Cognito.Extensions;

namespace Granit.Identity.Federated.Cognito;

/// <summary>
/// Granit module that registers the AWS Cognito User Pools as the
/// <see cref="IIdentityProvider"/> implementation.
/// </summary>
[DependsOn(typeof(GranitIdentityFederatedModule))]
public sealed class GranitIdentityFederatedCognitoModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.AddGranitIdentityCognito();
}
