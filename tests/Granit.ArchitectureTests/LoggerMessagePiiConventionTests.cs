using Granit.ArchitectureTests.Abstractions.Rules;
using Xunit;

namespace Granit.ArchitectureTests;

/// <summary>
/// Validates that [LoggerMessage] template parameters do not expose PII-indicative names.
/// Logic lives in <c>Granit.ArchitectureTests.Abstractions</c> so downstream repos can reuse it.
/// </summary>
public sealed class LoggerMessagePiiConventionTests
{
    private static readonly string RepoRoot =
        OpenApiTagConventionRules.FindRepoRoot(typeof(LoggerMessagePiiConventionTests).Assembly);

    /// <summary>
    /// Known exemptions — parameters confirmed to receive redacted values at all call sites.
    /// Format: <c>"ClassName.ParameterName"</c>. Each entry must carry an inline justification.
    /// </summary>
    private static readonly HashSet<string> Exemptions = new(StringComparer.Ordinal)
    {
        // GUIDs — pseudonymous identifiers (GDPR Recital 26), standard OTel practice.
        "KeycloakIdentityProvider.UserId",
        "EntraIdIdentityProvider.UserId",
        "CognitoIdentityProvider.UserId",
        "GoogleCloudIdentityProvider.UserId",
        "NullPasswordResetNotifier.UserId",
        "AspNetImpersonationService.ImpersonatorId",
        "AspNetImpersonationService.TargetUserId",
        "MobilePushNotificationChannel.UserId",
        "BackChannelLogoutTokenValidator.SessionId",
        "BackChannelLogoutTokenValidator.SubjectId",
        "BffLoginEndpoints.SessionId",
        "DefaultBffLogoutOrchestrator.SessionId",
        "BffBackChannelLogoutEndpoints.Subject",
        "ConnectAuthorizationEndpoints.Subject",
        "ConnectTokenEndpoints.Subject",
        "BffTokenInjectionTransform.SessionId",
        "BffTokenInjectionMiddleware.SessionId",
        "KeycloakUserTokenExchangeService.UserId",
        "BackgroundJobManager.UserId",
        // Session IDs — opaque internal identifiers, not direct PII.
        "EntraIdIdentityProvider.SessionId",
        "KeycloakIdentityProvider.SessionId",
        "PkceState.SessionId",
        "DistributedCacheRevokedSessionStore.SessionId",
        // Infrastructure address — Vault server URL, not a personal address.
        "VaultClientFactory.Address",
    };

    [Fact]
    public void LoggerMessage_parameters_should_not_contain_pii_names() =>
        PiiConventionRules.LoggerMessageParametersShouldNotContainPiiNames(
            Path.Join(RepoRoot, "src"),
            RepoRoot,
            Exemptions);
}
