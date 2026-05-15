using Granit.Webhooks.Endpoints.Permissions;
using Granit.Workspaces;

namespace Granit.Webhooks.Endpoints.Workspaces;

/// <summary>Declares the webhooks module's features (per ADR-057).</summary>
internal sealed class WebhooksFeatureProvider : IFeatureProvider
{
    /// <inheritdoc />
    public void DefineFeatures(IFeatureCatalogBuilder catalog) =>
        catalog.Add(WebhooksFeatures.Subscriptions, f => f
            .Permission(WebhooksPermissions.Subscriptions.Read)
            .RouteName(WebhooksFeatures.Subscriptions)
            .DefaultIcon("webhook")
            .DisplayKey("WebhooksEndpoints:Workspace.Item"));
}
