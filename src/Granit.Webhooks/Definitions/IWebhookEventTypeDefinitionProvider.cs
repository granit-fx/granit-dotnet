namespace Granit.Webhooks.Definitions;

/// <summary>
/// Extension point allowing a module to declare the webhook event types it supports.
/// Implementations are auto-discovered across loaded module assemblies.
/// </summary>
public interface IWebhookEventTypeDefinitionProvider
{
    /// <summary>
    /// Declares webhook event types via <paramref name="context"/>.
    /// </summary>
    void Define(IWebhookEventTypeDefinitionContext context);
}
