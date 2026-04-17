using Granit.Authorization.Extensions;
using Granit.Guids;
using Granit.Payments.Contracts;
using Granit.Payments.Domain;
using Granit.Payments.Endpoints.Dtos;
using Granit.Payments.Endpoints.Internal;
using Granit.Payments.Endpoints.Permissions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using ZiggyCreatures.Caching.Fusion;

namespace Granit.Payments.Endpoints.Endpoints;

internal static class PaymentMethodConfigurationEndpoints
{
    private const string ResolverCacheKey = "granit:payments:active-configs";

    internal static RouteGroupBuilder MapPaymentMethodConfigurationEndpoints(
        this RouteGroupBuilder group)
    {
        RouteGroupBuilder config = group.MapGroup("/configuration");

        config.MapGet("/", GetAllAsync)
            .WithName("ListPaymentMethodConfigurations")
            .WithSummary("Lists all payment methods declared by installed providers with their activation state.")
            .WithDescription(
                "Returns the fused view of all payment methods supported by registered IPaymentProvider "
                + "instances, grouped by provider. Each method shows its localized display label, category "
                + "and current activation state. Use the /{provider}/{method}/activate and /deactivate "
                + "endpoints to toggle a method.")
            .Produces<IReadOnlyList<PaymentProviderConfigurationResponse>>()
            .RequireAuthorization(PaymentsPermissions.Configuration.Manage)
            .AllowHostAccess();

        config.MapPost("/{providerName}/{methodType}/activate", ActivateAsync)
            .WithName("ActivatePaymentMethodConfiguration")
            .WithSummary("Activates a payment method for the platform.")
            .WithDescription(
                "Marks the method declared by the given provider as active. Idempotent: calling on an "
                + "already-active method returns 200 OK with no change. Returns 404 if the provider is "
                + "not registered, 400 if the provider does not declare the given method type.")
            .Produces<PaymentMethodConfigurationItem>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization(PaymentsPermissions.Configuration.Manage)
            .AllowHostAccess();

        config.MapPost("/{providerName}/{methodType}/deactivate", DeactivateAsync)
            .WithName("DeactivatePaymentMethodConfiguration")
            .WithSummary("Deactivates a payment method for the platform.")
            .WithDescription(
                "Marks the method as inactive. Tenants will no longer see it in GET /methods/available. "
                + "Idempotent: calling on an already-inactive (or never-activated) method returns 200 OK.")
            .Produces<PaymentMethodConfigurationItem>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization(PaymentsPermissions.Configuration.Manage)
            .AllowHostAccess();

        return group;
    }

    // -------------------------------------------------------------------------
    // GET / — list all declared methods × activation state (grouped by provider)
    // -------------------------------------------------------------------------

    private static async Task<Ok<IReadOnlyList<PaymentProviderConfigurationResponse>>> GetAllAsync(
        [FromServices] IEnumerable<IPaymentProvider> providers,
        [FromServices] IPaymentMethodConfigurationReader reader,
        [FromServices] IStringLocalizer<PaymentsEndpointsLocalizationResource> localizer,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<PaymentMethodConfiguration> configs = await reader
            .GetAllAsync(cancellationToken).ConfigureAwait(false);

        var activeByKey = configs.ToDictionary(
            c => (c.ProviderName, c.MethodType),
            c => c.IsActive,
            new ProviderMethodKeyComparer());

        var response = providers
            .Select(p => new PaymentProviderConfigurationResponse(
                p.Name,
                p.SupportedMethods
                    .Select(d => new PaymentMethodConfigurationItem(
                        d.MethodType,
                        PaymentMethodLabelResolver.Resolve(localizer, d.MethodType),
                        d.Category,
                        activeByKey.TryGetValue((p.Name, d.MethodType), out bool active) && active))
                    .ToList()))
            .OrderBy(g => g.ProviderName)
            .ToList();

        return TypedResults.Ok<IReadOnlyList<PaymentProviderConfigurationResponse>>(response);
    }

    // -------------------------------------------------------------------------
    // POST /{provider}/{method}/activate
    // -------------------------------------------------------------------------

    private static Task<Results<Ok<PaymentMethodConfigurationItem>, ProblemHttpResult>> ActivateAsync(
        string providerName,
        string methodType,
        [FromServices] IEnumerable<IPaymentProvider> providers,
        [FromServices] IPaymentMethodConfigurationReader reader,
        [FromServices] IPaymentMethodConfigurationWriter writer,
        [FromServices] IGuidGenerator guidGenerator,
        [FromServices] IStringLocalizer<PaymentsEndpointsLocalizationResource> localizer,
        [FromServices] IFusionCache cache,
        CancellationToken cancellationToken) =>
        ToggleAsync(providerName, methodType, activate: true,
            providers, reader, writer, guidGenerator, localizer, cache, cancellationToken);

    // -------------------------------------------------------------------------
    // POST /{provider}/{method}/deactivate
    // -------------------------------------------------------------------------

    private static Task<Results<Ok<PaymentMethodConfigurationItem>, ProblemHttpResult>> DeactivateAsync(
        string providerName,
        string methodType,
        [FromServices] IEnumerable<IPaymentProvider> providers,
        [FromServices] IPaymentMethodConfigurationReader reader,
        [FromServices] IPaymentMethodConfigurationWriter writer,
        [FromServices] IGuidGenerator guidGenerator,
        [FromServices] IStringLocalizer<PaymentsEndpointsLocalizationResource> localizer,
        [FromServices] IFusionCache cache,
        CancellationToken cancellationToken) =>
        ToggleAsync(providerName, methodType, activate: false,
            providers, reader, writer, guidGenerator, localizer, cache, cancellationToken);

    // -------------------------------------------------------------------------
    // Shared toggle logic
    // -------------------------------------------------------------------------

    private static async Task<Results<Ok<PaymentMethodConfigurationItem>, ProblemHttpResult>> ToggleAsync(
        string providerName,
        string methodType,
        bool activate,
        [FromServices] IEnumerable<IPaymentProvider> providers,
        [FromServices] IPaymentMethodConfigurationReader reader,
        [FromServices] IPaymentMethodConfigurationWriter writer,
        [FromServices] IGuidGenerator guidGenerator,
        [FromServices] IStringLocalizer<PaymentsEndpointsLocalizationResource> localizer,
        [FromServices] IFusionCache cache,
        CancellationToken cancellationToken)
    {
        // 1. Validate provider exists in DI
        IPaymentProvider? provider = providers
            .FirstOrDefault(p => p.Name.Equals(providerName, StringComparison.OrdinalIgnoreCase));

        if (provider is null)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status404NotFound,
                detail: $"Payment provider '{providerName}' is not registered.");
        }

        // 2. Validate method is declared by the provider
        PaymentMethodDescriptor? descriptor = provider.SupportedMethods
            .FirstOrDefault(d => d.MethodType.Equals(methodType, StringComparison.OrdinalIgnoreCase));

        if (descriptor is null)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                detail: $"Provider '{providerName}' does not support method type '{methodType}'.");
        }

        // 3. Race-safe upsert — the store handles DbUpdateException internally
        await writer.UpsertActivationAsync(
            guidGenerator.Create(),
            provider.Name,
            descriptor.MethodType,
            activate,
            cancellationToken).ConfigureAwait(false);

        // 4. Proactive cache invalidation — tenants see the change immediately
        await cache.RemoveAsync(ResolverCacheKey, token: cancellationToken).ConfigureAwait(false);

        // 5. Return the merged view for this method
        var item = new PaymentMethodConfigurationItem(
            descriptor.MethodType,
            PaymentMethodLabelResolver.Resolve(localizer, descriptor.MethodType),
            descriptor.Category,
            activate);

        return TypedResults.Ok(item);
    }

    private sealed class ProviderMethodKeyComparer : IEqualityComparer<(string Provider, string Method)>
    {
        public bool Equals((string Provider, string Method) x, (string Provider, string Method) y) =>
            string.Equals(x.Provider, y.Provider, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(x.Method, y.Method, StringComparison.OrdinalIgnoreCase);

        public int GetHashCode((string Provider, string Method) obj) =>
            HashCode.Combine(
                obj.Provider.ToLowerInvariant(),
                obj.Method.ToLowerInvariant());
    }
}
