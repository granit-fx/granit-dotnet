using Granit.Webhooks.Definitions;
using Granit.Webhooks.Endpoints.Dtos;
using Granit.Webhooks.Endpoints.Permissions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

namespace Granit.Webhooks.Endpoints.Endpoints;

internal static class WebhookEventTypeEndpoints
{
    internal static RouteGroupBuilder MapEventTypeEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/event-types", GetEventTypes)
            .WithName("GetWebhookEventTypes")
            .WithSummary("Returns all registered webhook event types.")
            .WithDescription(
                "Returns the full list of webhook event types declared by application modules at startup. "
                + "Each entry includes the event type name, localized display name, description, and category for UI grouping. "
                + "Labels are resolved based on the Accept-Language header. "
                + "Use these values when creating webhook subscriptions to ensure the event type is valid. "
                + "Returns an empty list if no event type providers are registered.")
            .Produces<IReadOnlyList<WebhookEventTypeResponse>>()
            .RequireAuthorization(WebhooksPermissions.Subscriptions.Read);

        return group;
    }

    internal static Ok<IReadOnlyList<WebhookEventTypeResponse>> GetEventTypes(
        [FromServices] IWebhookEventTypeRegistry registry,
        HttpContext httpContext)
    {
        IStringLocalizerFactory? localizerFactory =
            httpContext.RequestServices.GetService<IStringLocalizerFactory>();

        IReadOnlyList<WebhookEventTypeResponse> responses = [.. registry.GetAll()
            .Select(d => new WebhookEventTypeResponse(
                d.Name,
                d.DisplayName?.Localize(localizerFactory),
                d.Description?.Localize(localizerFactory),
                d.Category?.Localize(localizerFactory)))];

        return TypedResults.Ok(responses);
    }
}
