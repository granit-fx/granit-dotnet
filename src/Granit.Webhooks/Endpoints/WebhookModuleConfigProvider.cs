using Granit.Modularity;
using Granit.Webhooks.Dtos;
using Granit.Webhooks.Options;
using Microsoft.Extensions.Options;

namespace Granit.Webhooks.Endpoints;

/// <summary>
/// Maps <see cref="WebhooksOptions"/> to the public-facing <see cref="WebhookModuleConfigResponse"/>.
/// </summary>
internal sealed class WebhookModuleConfigProvider(IOptions<WebhooksOptions> options)
    : IModuleConfigProvider<WebhookModuleConfigResponse>
{
    public WebhookModuleConfigResponse GetConfig() =>
        new(options.Value.StorePayload);
}
