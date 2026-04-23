using Granit.BackgroundJobs;
using Granit.Identity.Federated.Cognito;
using Granit.Modularity;

namespace Granit.Identity.Federated.Cognito.BackgroundJobs;

/// <summary>
/// Registers the recurring Cognito app-client group sync job. The handler reuses the
/// Phase 2 <c>CognitoClientRoleSyncService</c> registered by
/// <c>AddGranitIdentityCognito</c> — no additional DI wiring required here.
/// </summary>
[DependsOn(
    typeof(GranitBackgroundJobsModule),
    typeof(GranitIdentityFederatedCognitoModule))]
public sealed class GranitIdentityFederatedCognitoBackgroundJobsModule : GranitModule;
