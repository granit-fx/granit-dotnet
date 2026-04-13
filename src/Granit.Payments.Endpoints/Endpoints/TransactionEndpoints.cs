using Granit.Http.Idempotency.Attributes;
using Granit.MultiTenancy;
using Granit.Payments.Commands;
using Granit.Payments.Domain;
using Granit.Payments.Endpoints.Dtos;
using Granit.Payments.Endpoints.Permissions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Wolverine;
using ContractCheckoutRequest = Granit.Payments.Contracts.PaymentCheckoutSessionRequest;

namespace Granit.Payments.Endpoints.Endpoints;

internal static class TransactionEndpoints
{
    internal static RouteGroupBuilder MapTransactionEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/transactions", GetForTenantAsync)
            .WithName("ListPaymentTransactions")
            .WithSummary("Lists all payment transactions for the current tenant.")
            .WithDescription(
                "Returns every payment transaction belonging to the current tenant, ordered by creation date. "
                + "Each transaction includes its refunds and disputes. "
                + "Requires the Payments.Transactions.Read permission.")
            .Produces<IReadOnlyList<PaymentTransactionResponse>>()
            .RequireAuthorization(PaymentsPermissions.Transactions.Read);

        group.MapGet("/transactions/{id:guid}", GetByIdAsync)
            .WithName("GetPaymentTransaction")
            .WithSummary("Returns a payment transaction by its unique identifier.")
            .WithDescription(
                "Fetches the full details of a single payment transaction including its status, "
                + "provider reference, refunds, and disputes. "
                + "Returns 404 if the transaction does not exist.")
            .Produces<PaymentTransactionResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization(PaymentsPermissions.Transactions.Read);

        group.MapPost("/charge", ChargeAsync)
            .WithName("InitiatePaymentCharge")
            .WithSummary("Initiates a payment charge for an invoice.")
            .WithDescription(
                "Dispatches an InitiatePaymentCommand to charge the specified invoice amount "
                + "using the given payment method type. The charge is processed asynchronously "
                + "and the transaction can be tracked via the transactions endpoints. "
                + "Returns 202 Accepted when the command has been dispatched.")
            .WithMetadata(new IdempotentAttribute())
            .Produces(StatusCodes.Status202Accepted)
            .ProducesValidationProblem()
            .RequireAuthorization(PaymentsPermissions.Charges.Execute);

        group.MapPost("/refund", RefundAsync)
            .WithName("RequestPaymentRefund")
            .WithSummary("Requests a refund for a payment transaction.")
            .WithDescription(
                "Dispatches a RequestRefundCommand for the specified transaction. Partial refunds "
                + "are supported by specifying an amount less than the original charge. "
                + "The refund is processed asynchronously via the payment provider. "
                + "Returns 202 Accepted when the command has been dispatched.")
            .WithMetadata(new IdempotentAttribute())
            .Produces(StatusCodes.Status202Accepted)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization(PaymentsPermissions.Refunds.Execute);

        group.MapPost("/checkout", CheckoutAsync)
            .WithName("CreateCheckoutSession")
            .WithSummary("Creates a hosted checkout session for a transaction.")
            .WithDescription(
                "Generates a hosted payment page URL via the payment provider. The client should "
                + "redirect the user to the returned URL. On completion, the provider redirects "
                + "to the success or cancel URL. Returns the session details including URL and expiry.")
            .WithMetadata(new IdempotentAttribute())
            .Produces<PaymentCheckoutSessionResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization(PaymentsPermissions.Charges.Execute);

        return group;
    }

    private static async Task<Ok<IReadOnlyList<PaymentTransactionResponse>>> GetForTenantAsync(
        [FromServices] IPaymentTransactionReader reader,
        [FromServices] ICurrentTenant currentTenant,
        CancellationToken cancellationToken)
    {
        Guid tenantId = currentTenant.Id ?? Guid.Empty;

        IReadOnlyList<PaymentTransaction> transactions = await reader
            .GetForInvoiceAsync(tenantId, cancellationToken)
            .ConfigureAwait(false);

        IReadOnlyList<PaymentTransactionResponse> response = transactions
            .Select(MapToResponse)
            .ToList();

        return TypedResults.Ok(response);
    }

    private static async Task<Results<Ok<PaymentTransactionResponse>, NotFound>> GetByIdAsync(
        Guid id,
        [FromServices] IPaymentTransactionReader reader,
        [FromServices] ICurrentTenant currentTenant,
        CancellationToken cancellationToken)
    {
        PaymentTransaction? transaction = await reader
            .GetByIdAsync(id, cancellationToken)
            .ConfigureAwait(false);

        if (transaction is null || transaction.TenantId != currentTenant.Id)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.Ok(MapToResponse(transaction));
    }

    private static async Task<Accepted> ChargeAsync(
        PaymentChargeRequest request,
        [FromHeader(Name = "Idempotency-Key")] string idempotencyKey,
        [FromServices] IMessageBus messageBus,
        [FromServices] ICurrentTenant currentTenant)
    {
        Guid tenantId = currentTenant.Id ?? Guid.Empty;

        var command = new InitiatePaymentCommand(
            request.InvoiceId,
            tenantId,
            request.Amount,
            request.Currency,
            request.MethodType,
            idempotencyKey,
            request.ProviderName);

        await messageBus.SendAsync(command).ConfigureAwait(false);

        return TypedResults.Accepted((string?)null);
    }

    private static async Task<Results<Accepted, NotFound>> RefundAsync(
        PaymentRefundRequest request,
        [FromHeader(Name = "Idempotency-Key")] string idempotencyKey,
        [FromServices] IMessageBus messageBus)
    {
        var command = new RequestRefundCommand(
            request.TransactionId,
            request.Amount,
            request.Reason,
            idempotencyKey);

        await messageBus.SendAsync(command).ConfigureAwait(false);

        return TypedResults.Accepted((string?)null);
    }

    private static async Task<Results<Created<PaymentCheckoutSessionResponse>, NotFound>> CheckoutAsync(
        PaymentCheckoutRequest request,
        [FromServices] IEnumerable<ICheckoutSessionFactory> factories,
        [FromServices] IPaymentProviderResolver resolver,
        [FromServices] ICurrentTenant currentTenant,
        CancellationToken cancellationToken)
    {
        Guid tenantId = currentTenant.Id ?? Guid.Empty;
        string providerName = request.ProviderName
            ?? resolver.Resolve(tenantId, request.MethodType).Name;

        ICheckoutSessionFactory? factory = factories
            .FirstOrDefault(f => f.ProviderName.Equals(providerName, StringComparison.OrdinalIgnoreCase));

        if (factory is null)
        {
            return TypedResults.NotFound();
        }

        Granit.Payments.Contracts.PaymentCheckoutSession session = await factory
            .CreateAsync(
                new ContractCheckoutRequest(
                    request.TransactionId,
                    request.Amount,
                    request.Currency,
                    request.MethodType,
                    request.SuccessUrl,
                    request.CancelUrl),
                cancellationToken)
            .ConfigureAwait(false);

        var response = new PaymentCheckoutSessionResponse(
            session.Url, session.SessionId, session.ExpiresAt);

        return TypedResults.Created(session.Url, response);
    }

    internal static PaymentTransactionResponse MapToResponse(PaymentTransaction t) =>
        new(
            t.Id,
            t.InvoiceId,
            t.Amount,
            t.Currency,
            t.Status,
            t.ProviderName,
            t.ProviderTransactionId,
            t.PaymentMethodId,
            t.ActionUrl,
            t.IdempotencyKey,
            t.FailureCode,
            t.SucceededAt,
            t.CanceledAt,
            t.Refunds.Select(MapRefundToResponse).ToList(),
            t.Disputes.Select(MapDisputeToResponse).ToList(),
            t.TenantId);

    private static PaymentRefundResponse MapRefundToResponse(Refund r) =>
        new(
            r.Id,
            r.Amount,
            r.Currency,
            r.Status,
            r.ProviderRefundId,
            r.Reason,
            r.CreatedAt,
            r.CompletedAt);

    private static PaymentDisputeResponse MapDisputeToResponse(Dispute d) =>
        new(
            d.Id,
            d.ProviderDisputeId,
            d.Status,
            d.Reason,
            d.Amount,
            d.Currency,
            d.CreatedAt,
            d.ResolvedAt);
}
