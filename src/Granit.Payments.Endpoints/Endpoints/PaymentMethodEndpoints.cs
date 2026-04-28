using System.Diagnostics.CodeAnalysis;
using Granit.Authorization.Extensions;
using Granit.Guids;
using Granit.Http.Idempotency.Attributes;
using Granit.MultiTenancy;
using Granit.Parties;
using Granit.Parties.Domain;
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
using ContractAttachRequest = Granit.Payments.Contracts.PaymentAttachMethodRequest;

namespace Granit.Payments.Endpoints.Endpoints;

internal static class PaymentMethodEndpoints
{
    internal static RouteGroupBuilder MapPaymentMethodEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/methods", GetForTenantAsync)
            .WithName("ListPaymentMethods")
            .WithSummary("Lists saved payment methods for the current tenant.")
            .WithDescription(
                "Returns all payment methods that have been attached to the current tenant, "
                + "including their type, provider, display label, and default status. "
                + "Requires the Payments.Methods.Read permission.")
            .Produces<IReadOnlyList<PaymentMethodResponse>>()
            .RequireAuthorization(PaymentsPermissions.Methods.Read)
            .AllowHostAccess();

        group.MapGet("/methods/available", GetAvailableAsync)
            .WithName("GetAvailablePaymentMethods")
            .WithSummary("Lists payment methods available for the current tenant and request context.")
            .WithDescription(
                "Returns the payment methods that the tenant can use, filtered against the request "
                + "context (billing country, currency, amount, sequence type). Each method returns its "
                + "capability snapshot so the front-end can render explanatory UI "
                + "(e.g., 'available in BE only', Klarna amount bounds). Query params are optional: an "
                + "absent axis skips filtering on that axis. Returns 400 for malformed country/currency/"
                + "amount/sequenceType values.")
            .Produces<IReadOnlyList<PaymentAvailableMethodResponse>>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .RequireAuthorization(PaymentsPermissions.Methods.Read)
            .AllowHostAccess();

        group.MapPost("/methods", AttachAsync)
            .WithName("AttachPaymentMethod")
            .WithSummary("Attaches a payment method to the current tenant.")
            .WithDescription(
                "Creates a payment method on the provider using the supplied token (e.g. a Stripe "
                + "SetupIntent confirmation token) and persists it as a saved method for the tenant. "
                + "Returns the newly created payment method. "
                + "Requires the Payments.Methods.Manage permission.")
            .WithMetadata(new IdempotentAttribute())
            .Produces<PaymentMethodResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization(PaymentsPermissions.Methods.Manage)
            .AllowHostAccess();

        group.MapDelete("/methods/{id:guid}", DetachAsync)
            .WithName("DetachPaymentMethod")
            .WithSummary("Detaches a payment method from the current tenant.")
            .WithDescription(
                "Removes the payment method from the provider and deletes the local record. "
                + "Returns 204 No Content on success, or 404 if the method does not exist.")
            .WithMetadata(new IdempotentAttribute())
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization(PaymentsPermissions.Methods.Manage)
            .AllowHostAccess();

        return group;
    }

    private static async Task<Ok<IReadOnlyList<PaymentMethodResponse>>> GetForTenantAsync(
        [FromServices] IPaymentMethodReader reader,
        [FromServices] ICurrentTenant currentTenant,
        CancellationToken cancellationToken)
    {
        Guid tenantId = currentTenant.Id ?? Guid.Empty;

        IReadOnlyList<PaymentMethod> methods = await reader
            .GetForTenantAsync(tenantId, cancellationToken)
            .ConfigureAwait(false);

        IReadOnlyList<PaymentMethodResponse> response = methods
            .Select(MapToResponse)
            .ToList();

        return TypedResults.Ok(response);
    }

    [SuppressMessage("Major Code Smell", "S107:Methods should not have too many parameters", Justification = "Minimal-API endpoint — ASP.NET binds [FromServices]/[FromQuery] parameters explicitly.")]
    private static async Task<Results<Ok<IReadOnlyList<PaymentAvailableMethodResponse>>, ProblemHttpResult>> GetAvailableAsync(
        [FromServices] IPaymentProviderResolver resolver,
        [FromServices] ICurrentTenant currentTenant,
        [FromServices] IStringLocalizer<PaymentsEndpointsLocalizationResource> localizer,
        [FromQuery] string? country,
        [FromQuery] string? currency,
        [FromQuery] decimal? amount,
        [FromQuery] string? sequenceType,
        CancellationToken cancellationToken)
    {
        PaymentAvailabilityContext? context =
            PaymentAvailabilityContextParser.TryParse(country, currency, amount, sequenceType, out string? error);

        if (error is not null)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status400BadRequest, detail: error);
        }

        Guid tenantId = currentTenant.Id ?? Guid.Empty;

        IReadOnlyList<PaymentAvailableMethod> available = await resolver
            .GetAvailableProvidersAsync(tenantId, context, cancellationToken)
            .ConfigureAwait(false);

        IReadOnlyList<PaymentAvailableMethodResponse> response = available
            .Select(a => new PaymentAvailableMethodResponse(
                a.MethodType,
                a.Category,
                a.ProviderName,
                PaymentMethodLabelResolver.Resolve(localizer, a.MethodType),
                PaymentMethodCapabilityMapper.ToResponseOrNull(a.Capability)))
            .ToList();

        return TypedResults.Ok<IReadOnlyList<PaymentAvailableMethodResponse>>(response);
    }

    [SuppressMessage("Major Code Smell", "S107:Methods should not have too many parameters", Justification = "Minimal-API endpoint — ASP.NET binds [FromServices] parameters explicitly.")]
    private static async Task<Results<Created<PaymentMethodResponse>, ProblemHttpResult>> AttachAsync(
        Dtos.PaymentAttachMethodRequest request,
        [FromServices] IEnumerable<IPaymentMethodManager> managers,
        [FromServices] IPaymentMethodWriter writer,
        [FromServices] IGuidGenerator guidGenerator,
        [FromServices] IDefaultPartyResolver partyResolver,
        [FromServices] ICurrentTenant currentTenant,
        CancellationToken cancellationToken)
    {
        Guid tenantId = currentTenant.Id ?? Guid.Empty;

        IPaymentMethodManager? manager = managers
            .FirstOrDefault(m => m.ProviderName.Equals(request.ProviderName, StringComparison.OrdinalIgnoreCase));

        if (manager is null)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status404NotFound);
        }

        // Resolve the host-scoped party representing this tenant. Future iterations may
        // accept an explicit PartyId in the request DTO when a tenant has multiple parties.
        Party? party = tenantId == Guid.Empty
            ? null
            : await partyResolver
                .GetDefaultForTenantAsync(tenantId, cancellationToken)
                .ConfigureAwait(false);

        if (party is null)
        {
            return TypedResults.Problem(
                detail: "No default party resolved for the active tenant. Provision the host-scoped Party representing this tenant before attaching a payment method (see Granit.Parties.MultiTenancy).",
                statusCode: StatusCodes.Status409Conflict);
        }

        PaymentProviderMethod providerMethod = await manager
            .AttachAsync(
                new ContractAttachRequest(party.Id, request.Type, request.Token),
                cancellationToken)
            .ConfigureAwait(false);

        var method = PaymentMethod.Create(
            guidGenerator.Create(),
            tenantId,
            request.Type,
            request.ProviderName,
            providerMethod.ProviderMethodId,
            providerMethod.DisplayLabel,
            providerMethod.ExpiresAt);

        await writer.AddAsync(method, cancellationToken).ConfigureAwait(false);

        PaymentMethodResponse response = MapToResponse(method);
        return TypedResults.Created($"/methods/{method.Id}", response);
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> DetachAsync(
        Guid id,
        [FromServices] IPaymentMethodReader reader,
        [FromServices] IPaymentMethodWriter writer,
        [FromServices] IEnumerable<IPaymentMethodManager> managers,
        [FromServices] ICurrentTenant currentTenant,
        CancellationToken cancellationToken)
    {
        PaymentMethod? method = await reader
            .GetByIdAsync(id, cancellationToken)
            .ConfigureAwait(false);

        if (method is null || method.TenantId != currentTenant.Id)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status404NotFound);
        }

        IPaymentMethodManager? manager = managers
            .FirstOrDefault(m => m.ProviderName.Equals(method.ProviderName, StringComparison.OrdinalIgnoreCase));

        if (manager is not null)
        {
            await manager
                .DetachAsync(method.ProviderMethodId, cancellationToken)
                .ConfigureAwait(false);
        }

        await writer.DeleteAsync(method, cancellationToken).ConfigureAwait(false);

        return TypedResults.NoContent();
    }

    internal static PaymentMethodResponse MapToResponse(PaymentMethod m) =>
        new(
            m.Id,
            m.Type,
            m.ProviderName,
            m.ProviderMethodId,
            m.DisplayLabel,
            m.IsDefault,
            m.ExpiresAt,
            m.TenantId);
}
