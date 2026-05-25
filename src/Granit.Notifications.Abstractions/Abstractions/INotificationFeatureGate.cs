namespace Granit.Notifications.Abstractions;

/// <summary>
/// Optional adapter the <c>GET /notifications/types</c> endpoint resolves to
/// decide whether a <see cref="NotificationDefinition.RequiredFeature"/> is
/// enabled for the current tenant.
/// </summary>
/// <remarks>
/// <para>
/// Not registered by default. Hosts that want feature-gated visibility of
/// notification preferences ship their own implementation — typically a thin
/// adapter over <c>Granit.Features.IFeatureChecker</c>:
/// </para>
/// <code>
/// services.AddSingleton&lt;INotificationFeatureGate, FeaturesGateAdapter&gt;();
/// </code>
/// <para>
/// When no implementation is registered, the endpoint skips the feature filter
/// and shows definitions regardless of their <see cref="NotificationDefinition.RequiredFeature"/>.
/// </para>
/// </remarks>
public interface INotificationFeatureGate
{
    /// <summary>
    /// Returns <c>true</c> when the feature named <paramref name="featureName"/>
    /// is enabled for the current tenant / plan context.
    /// </summary>
    Task<bool> IsFeatureEnabledAsync(string featureName, CancellationToken cancellationToken = default);
}
