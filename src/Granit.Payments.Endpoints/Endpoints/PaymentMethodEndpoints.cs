using Granit.Guids;
using Granit.Http.Idempotency.Attributes;
using Granit.MultiTenancy;
using Granit.Payments.Contracts;
using Granit.Payments.Domain;
using Granit.Payments.Endpoints.Dtos;
using Granit.Payments.Endpoints.Permissions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
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
            .RequireAuthorization(PaymentsPermissions.Methods.Read);

        group.MapGet("/methods/available", GetAvailable)
            .WithName("GetAvailablePaymentMethods")
            .WithSummary("Lists payment methods available for the current tenant.")
            .WithDescription(
                "Returns the payment methods that the tenant can use based on the configured "
                + "providers. This includes method types, categories, and display labels as "
                + "reported by the active payment providers.")
            .Produces<IReadOnlyList<PaymentAvailableMethodResponse>>()
            .RequireAuthorization(PaymentsPermissions.Methods.Read);

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
            .RequireAuthorization(PaymentsPermissions.Methods.Manage);

        group.MapDelete("/methods/{id:guid}", DetachAsync)
            .WithName("DetachPaymentMethod")
            .WithSummary("Detaches a payment method from the current tenant.")
            .WithDescription(
                "Removes the payment method from the provider and deletes the local record. "
                + "Returns 204 No Content on success, or 404 if the method does not exist.")
            .WithMetadata(new IdempotentAttribute())
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization(PaymentsPermissions.Methods.Manage);

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

    private static Ok<IReadOnlyList<PaymentAvailableMethodResponse>> GetAvailable(
        [FromServices] IPaymentProviderResolver resolver,
        [FromServices] ICurrentTenant currentTenant)
    {
        Guid tenantId = currentTenant.Id ?? Guid.Empty;

        IReadOnlyList<PaymentAvailableMethod> available = resolver.GetAvailableProviders(tenantId);

        IReadOnlyList<PaymentAvailableMethodResponse> response = available
            .Select(a => new PaymentAvailableMethodResponse(
                a.MethodType, a.Category, a.ProviderName, a.DisplayLabel))
            .ToList();

        return TypedResults.Ok(response);
    }

    private static async Task<Results<Created<PaymentMethodResponse>, ProblemHttpResult>> AttachAsync(
        Dtos.PaymentAttachMethodRequest request,
        [FromServices] IEnumerable<IPaymentMethodManager> managers,
        [FromServices] IPaymentMethodWriter writer,
        [FromServices] IGuidGenerator guidGenerator,
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

        PaymentProviderMethod providerMethod = await manager
            .AttachAsync(
                new ContractAttachRequest(tenantId, request.Type, request.Token),
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
