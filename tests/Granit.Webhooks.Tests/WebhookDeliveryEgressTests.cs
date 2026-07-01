using System.Reflection;
using Granit.Webhooks.Extensions;
using Granit.Webhooks.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;
using Xunit;

namespace Granit.Webhooks.Tests;

/// <summary>
/// Egress hardening for the real, DI-configured webhook delivery pipeline
/// (<c>WebhooksConstants.HttpClientName</c>). Rather than a loopback round-trip — impossible here
/// because <see cref="WebhookSsrfConnectCallback"/> deliberately blocks loopback/private targets —
/// these tests resolve the ACTUAL primary <see cref="SocketsHttpHandler"/> that
/// <c>AddGranitWebhooks</c> wires through <c>ConfigurePrimaryHttpMessageHandler</c> (via
/// <see cref="IHttpMessageHandlerFactory"/>) and assert its egress settings directly:
/// <list type="bullet">
///   <item><c>ActivityHeadersPropagator == null</c> — no internal W3C trace context
///   (traceparent/tracestate) is emitted to customer-controlled URLs (VULN-100-OBS).</item>
///   <item><c>AllowAutoRedirect == false</c> — a subscriber 3xx is a delivery failure, never a
///   redirect to follow, which would also bypass the SSRF ConnectCallback (VULN-300-infra).</item>
/// </list>
/// This inspects the production handler instance, so a refactor that drops either setting fails here.
/// </summary>
public sealed class WebhookDeliveryEgressTests
{
    private static SocketsHttpHandler ResolvePrimaryDeliveryHandler(IServiceProvider services)
    {
        IHttpMessageHandlerFactory handlerFactory = services.GetRequiredService<IHttpMessageHandlerFactory>();
        HttpMessageHandler handler = handlerFactory.CreateHandler(WebhooksConstants.HttpClientName);

        // Walk the DelegatingHandler chain (resilience, logging, …) down to the primary handler.
        while (handler is DelegatingHandler delegating)
        {
            delegating.InnerHandler.ShouldNotBeNull(
                "The webhook delivery handler chain terminated without a primary handler.");
            handler = delegating.InnerHandler;
        }

        return handler.ShouldBeOfType<SocketsHttpHandler>(
            "AddGranitWebhooks must configure a SocketsHttpHandler as the primary webhook delivery handler "
            + "(it carries the SSRF ConnectCallback and the egress-hardening settings).");
    }

    private static ServiceProvider BuildWebhookHost()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.AddGranitWebhooks();
        return builder.Services.BuildServiceProvider();
    }

    [Fact]
    public void Delivery_handler_suppresses_trace_context_propagation()
    {
        using ServiceProvider provider = BuildWebhookHost();

        SocketsHttpHandler handler = ResolvePrimaryDeliveryHandler(provider);

        // ActivityHeadersPropagator has no public getter in all TFMs — read it reflectively.
        PropertyInfo? propagatorProperty = typeof(SocketsHttpHandler)
            .GetProperty("ActivityHeadersPropagator", BindingFlags.Public | BindingFlags.Instance);
        propagatorProperty.ShouldNotBeNull(
            "SocketsHttpHandler.ActivityHeadersPropagator not found — the runtime API changed; "
            + "revisit the webhook egress hardening.");

        object? propagator = propagatorProperty.GetValue(handler);
        propagator.ShouldBeNull(
            "Webhook delivery must null ActivityHeadersPropagator so internal W3C trace context "
            + "(traceparent/tracestate) never leaks to customer-controlled webhook URLs (VULN-100-OBS).");
    }

    [Fact]
    public void Delivery_handler_disables_auto_redirect()
    {
        using ServiceProvider provider = BuildWebhookHost();

        SocketsHttpHandler handler = ResolvePrimaryDeliveryHandler(provider);

        handler.AllowAutoRedirect.ShouldBeFalse(
            "Webhook delivery must set AllowAutoRedirect = false — a 3xx from a subscriber is a delivery "
            + "failure, and following it would bypass the SSRF ConnectCallback (VULN-300-infra).");
    }
}
