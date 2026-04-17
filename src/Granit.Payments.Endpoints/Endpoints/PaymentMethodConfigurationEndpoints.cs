using Granit.Authorization.Extensions;
using Granit.Guids;
using Granit.Http.Idempotency.Attributes;
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
                + "and current activation state plus the capability snapshot captured at activation time.")
            .Produces<IReadOnlyList<PaymentProviderConfigurationResponse>>()
            .RequireAuthorization(PaymentsPermissions.Configuration.Manage)
            .AllowHostAccess();

        config.MapGet("/catalog", GetCatalogAsync)
            .WithName("GetPaymentProviderCatalog")
            .WithSummary("Returns the live catalog of a specific provider with activation state.")
            .WithDescription(
                "Calls IPaymentProvider.GetCatalogAsync on the named provider and cross-references the "
                + "result with the current activation records. Use this endpoint to power admin UIs where "
                + "new provider methods should appear automatically (before any activation has snapshotted "
                + "them). Returns 404 if the provider is not registered.")
            .Produces<PaymentProviderCatalogResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization(PaymentsPermissions.Configuration.Manage)
            .AllowHostAccess();

        config.MapPost("/{providerName}/{methodType}/activate", ActivateAsync)
            .WithName("ActivatePaymentMethodConfiguration")
            .WithSummary("Activates a payment method for the platform and snapshots its capability.")
            .WithDescription(
                "Marks the method declared by the given provider as active and captures its current "
                + "capability (countries, currencies, amount bounds, sequence types) on the activation "
                + "record. Idempotent. Returns 404 if the provider is not registered, 400 if the provider "
                + "no longer offers the given method type.")
            .Produces<PaymentMethodConfigurationItem>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithMetadata(new IdempotentAttribute { Required = false })
            .RequireAuthorization(PaymentsPermissions.Configuration.Manage)
            .AllowHostAccess();

        config.MapPost("/{providerName}/{methodType}/deactivate", DeactivateAsync)
            .WithName("DeactivatePaymentMethodConfiguration")
            .WithSummary("Deactivates a payment method for the platform.")
            .WithDescription(
                "Marks the method as inactive. Tenants will no longer see it in GET /methods/available. "
                + "The capability snapshot is preserved so a future reactivation does not require a re-sync.")
            .Produces<PaymentMethodConfigurationItem>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithMetadata(new IdempotentAttribute { Required = false })
            .RequireAuthorization(PaymentsPermissions.Configuration.Manage)
            .AllowHostAccess();

        config.MapPost("/{providerName}/{methodType}/resync", ResyncAsync)
            .WithName("ResyncPaymentMethodConfiguration")
            .WithSummary("Re-fetches the provider catalog and updates the capability snapshot.")
            .WithDescription(
                "Calls IPaymentProvider.GetCatalogAsync and overwrites the capability snapshot on the "
                + "existing activation record. Use this to propagate provider-side changes (added "
                + "countries, updated amount bounds) without deactivating/reactivating the method.")
            .Produces<PaymentMethodConfigurationItem>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithMetadata(new IdempotentAttribute { Required = false })
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

        var configsByKey = configs.ToDictionary(
            c => (c.ProviderName, c.MethodType),
            c => c,
            new ProviderMethodKeyComparer());

        List<PaymentProviderConfigurationResponse> response = [];
        foreach (IPaymentProvider provider in providers.OrderBy(p => p.Name, StringComparer.Ordinal))
        {
            List<PaymentMethodConfigurationItem> methods = [];
            foreach (PaymentMethodDescriptor descriptor in provider.SupportedMethods)
            {
                configsByKey.TryGetValue((provider.Name, descriptor.MethodType), out PaymentMethodConfiguration? config);
                PaymentMethodCapability? snapshot = config?.GetCapabilitySnapshot();

                methods.Add(new PaymentMethodConfigurationItem(
                    MethodType: descriptor.MethodType,
                    DisplayLabel: PaymentMethodLabelResolver.Resolve(localizer, descriptor.MethodType),
                    Category: descriptor.Category,
                    Activated: config?.Activated ?? false,
                    CapabilitySnapshot: PaymentMethodCapabilityMapper.ToResponseOrNull(snapshot)));
            }

            response.Add(new PaymentProviderConfigurationResponse(provider.Name, methods));
        }

        return TypedResults.Ok<IReadOnlyList<PaymentProviderConfigurationResponse>>(response);
    }

    // -------------------------------------------------------------------------
    // GET /catalog?providerName=
    // -------------------------------------------------------------------------

    private static async Task<Results<Ok<PaymentProviderCatalogResponse>, ProblemHttpResult>> GetCatalogAsync(
        [FromQuery] string providerName,
        [FromServices] IEnumerable<IPaymentProvider> providers,
        [FromServices] IPaymentMethodConfigurationReader reader,
        CancellationToken cancellationToken)
    {
        IPaymentProvider? provider = providers
            .FirstOrDefault(p => p.Name.Equals(providerName, StringComparison.OrdinalIgnoreCase));

        if (provider is null)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status404NotFound,
                detail: $"Payment provider '{providerName}' is not registered.");
        }

        IReadOnlyList<PaymentMethodCatalogEntry> catalog = await provider
            .GetCatalogAsync(cancellationToken).ConfigureAwait(false);

        IReadOnlyList<PaymentMethodConfiguration> allConfigs = await reader
            .GetAllAsync(cancellationToken).ConfigureAwait(false);

        var configsForProvider = allConfigs
            .Where(c => c.ProviderName.Equals(provider.Name, StringComparison.OrdinalIgnoreCase))
            .ToDictionary(c => c.MethodType, c => c, StringComparer.OrdinalIgnoreCase);

        List<PaymentCatalogMethod> methods = [];
        foreach (PaymentMethodCatalogEntry entry in catalog)
        {
            configsForProvider.TryGetValue(entry.MethodType, out PaymentMethodConfiguration? config);

            methods.Add(new PaymentCatalogMethod(
                MethodType: entry.MethodType,
                Category: entry.Category,
                DisplayLabel: entry.DisplayLabel,
                Capability: PaymentMethodCapabilityMapper.ToResponse(entry.Capability),
                Activated: config?.Activated ?? false,
                HasSnapshot: config?.GetCapabilitySnapshot() is not null));
        }

        return TypedResults.Ok(new PaymentProviderCatalogResponse(provider.Name, methods));
    }

    // -------------------------------------------------------------------------
    // POST /{provider}/{method}/activate
    // -------------------------------------------------------------------------

    private static async Task<Results<Ok<PaymentMethodConfigurationItem>, ProblemHttpResult>> ActivateAsync(
        string providerName,
        string methodType,
        [FromServices] IEnumerable<IPaymentProvider> providers,
        [FromServices] IPaymentMethodConfigurationWriter writer,
        [FromServices] IGuidGenerator guidGenerator,
        [FromServices] IStringLocalizer<PaymentsEndpointsLocalizationResource> localizer,
        [FromServices] IFusionCache cache,
        CancellationToken cancellationToken)
    {
        IPaymentProvider? provider = providers
            .FirstOrDefault(p => p.Name.Equals(providerName, StringComparison.OrdinalIgnoreCase));

        if (provider is null)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status404NotFound,
                detail: $"Payment provider '{providerName}' is not registered.");
        }

        IReadOnlyList<PaymentMethodCatalogEntry> catalog = await provider
            .GetCatalogAsync(cancellationToken).ConfigureAwait(false);

        PaymentMethodCatalogEntry? entry = catalog
            .FirstOrDefault(e => e.MethodType.Equals(methodType, StringComparison.OrdinalIgnoreCase));

        if (entry is null)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                detail: $"Provider '{providerName}' does not currently offer method type '{methodType}'.");
        }

        await writer.UpsertActivationWithSnapshotAsync(
            guidGenerator.Create(),
            provider.Name,
            entry.MethodType,
            entry.Capability,
            cancellationToken).ConfigureAwait(false);

        await cache.RemoveAsync(ResolverCacheKey, token: cancellationToken).ConfigureAwait(false);

        return TypedResults.Ok(new PaymentMethodConfigurationItem(
            MethodType: entry.MethodType,
            DisplayLabel: PaymentMethodLabelResolver.Resolve(localizer, entry.MethodType),
            Category: entry.Category,
            Activated: true,
            CapabilitySnapshot: PaymentMethodCapabilityMapper.ToResponse(entry.Capability)));
    }

    // -------------------------------------------------------------------------
    // POST /{provider}/{method}/deactivate
    // -------------------------------------------------------------------------

    private static async Task<Results<Ok<PaymentMethodConfigurationItem>, ProblemHttpResult>> DeactivateAsync(
        string providerName,
        string methodType,
        [FromServices] IEnumerable<IPaymentProvider> providers,
        [FromServices] IPaymentMethodConfigurationReader reader,
        [FromServices] IPaymentMethodConfigurationWriter writer,
        [FromServices] IGuidGenerator guidGenerator,
        [FromServices] IStringLocalizer<PaymentsEndpointsLocalizationResource> localizer,
        [FromServices] IFusionCache cache,
        CancellationToken cancellationToken)
    {
        IPaymentProvider? provider = providers
            .FirstOrDefault(p => p.Name.Equals(providerName, StringComparison.OrdinalIgnoreCase));

        if (provider is null)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status404NotFound,
                detail: $"Payment provider '{providerName}' is not registered.");
        }

        PaymentMethodDescriptor? descriptor = provider.SupportedMethods
            .FirstOrDefault(d => d.MethodType.Equals(methodType, StringComparison.OrdinalIgnoreCase));

        if (descriptor is null)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                detail: $"Provider '{providerName}' does not support method type '{methodType}'.");
        }

        await writer.UpsertActivationAsync(
            guidGenerator.Create(),
            provider.Name,
            descriptor.MethodType,
            isActive: false,
            cancellationToken).ConfigureAwait(false);

        await cache.RemoveAsync(ResolverCacheKey, token: cancellationToken).ConfigureAwait(false);

        PaymentMethodConfiguration? config = await reader
            .FindAsync(provider.Name, descriptor.MethodType, cancellationToken).ConfigureAwait(false);

        return TypedResults.Ok(new PaymentMethodConfigurationItem(
            MethodType: descriptor.MethodType,
            DisplayLabel: PaymentMethodLabelResolver.Resolve(localizer, descriptor.MethodType),
            Category: descriptor.Category,
            Activated: false,
            CapabilitySnapshot: PaymentMethodCapabilityMapper.ToResponseOrNull(config?.GetCapabilitySnapshot())));
    }

    // -------------------------------------------------------------------------
    // POST /{provider}/{method}/resync
    // -------------------------------------------------------------------------

    private static async Task<Results<Ok<PaymentMethodConfigurationItem>, ProblemHttpResult>> ResyncAsync(
        string providerName,
        string methodType,
        [FromServices] IEnumerable<IPaymentProvider> providers,
        [FromServices] IPaymentMethodConfigurationReader reader,
        [FromServices] IPaymentMethodConfigurationWriter writer,
        [FromServices] IStringLocalizer<PaymentsEndpointsLocalizationResource> localizer,
        [FromServices] IFusionCache cache,
        CancellationToken cancellationToken)
    {
        IPaymentProvider? provider = providers
            .FirstOrDefault(p => p.Name.Equals(providerName, StringComparison.OrdinalIgnoreCase));

        if (provider is null)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status404NotFound,
                detail: $"Payment provider '{providerName}' is not registered.");
        }

        PaymentMethodConfiguration? existing = await reader
            .FindAsync(provider.Name, methodType, cancellationToken).ConfigureAwait(false);

        if (existing is null)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status404NotFound,
                detail: $"No configuration exists for '{providerName}'/'{methodType}'. Activate it first.");
        }

        IReadOnlyList<PaymentMethodCatalogEntry> catalog = await provider
            .GetCatalogAsync(cancellationToken).ConfigureAwait(false);

        PaymentMethodCatalogEntry? entry = catalog
            .FirstOrDefault(e => e.MethodType.Equals(methodType, StringComparison.OrdinalIgnoreCase));

        if (entry is null)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                detail: $"Provider '{providerName}' no longer offers method type '{methodType}'.");
        }

        await writer.UpdateCapabilitySnapshotAsync(
            provider.Name, entry.MethodType, entry.Capability, cancellationToken)
            .ConfigureAwait(false);

        await cache.RemoveAsync(ResolverCacheKey, token: cancellationToken).ConfigureAwait(false);

        return TypedResults.Ok(new PaymentMethodConfigurationItem(
            MethodType: entry.MethodType,
            DisplayLabel: PaymentMethodLabelResolver.Resolve(localizer, entry.MethodType),
            Category: entry.Category,
            Activated: existing.Activated,
            CapabilitySnapshot: PaymentMethodCapabilityMapper.ToResponse(entry.Capability)));
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
