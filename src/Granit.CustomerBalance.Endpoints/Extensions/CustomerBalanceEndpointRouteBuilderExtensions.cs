using Granit.Authorization.Extensions;
using Granit.CustomerBalance.Endpoints.Dtos;
using Granit.CustomerBalance.Endpoints.Internal;
using Granit.CustomerBalance.Endpoints.Options;
using Granit.CustomerBalance.Endpoints.Permissions;
using Granit.Validation.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Granit.CustomerBalance.Endpoints.Extensions;

/// <summary>Extension methods for registering customer balance endpoints.</summary>
public static class CustomerBalanceEndpointRouteBuilderExtensions
{
    /// <summary>Maps the customer balance endpoints.</summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <param name="configure">Optional delegate to customize <see cref="CustomerBalanceEndpointsOptions"/>.</param>
    /// <returns>The <see cref="RouteGroupBuilder"/> for further chaining.</returns>
    public static RouteGroupBuilder MapGranitCustomerBalance(
        this IEndpointRouteBuilder endpoints,
        Action<CustomerBalanceEndpointsOptions>? configure = null)
    {
        CustomerBalanceEndpointsOptions options = new();
        configure?.Invoke(options);

        RouteGroupBuilder group = endpoints
            .MapGranitGroup(options.RoutePrefix)
            .WithTags(options.TagName);

        group.MapGet("/balance", GetBalanceEndpoint.HandleAsync)
            .WithName("GetCustomerBalance")
            .WithSummary("Returns the balance for the current tenant and currency.")
            .WithDescription(
                "Returns the current credit balance for the authenticated tenant in the specified currency. "
                + "Returns zero balance if no account exists for that currency.")
            .Produces<CustomerBalanceResponse>()
            .RequireAuthorization(CustomerBalancePermissions.Accounts.Read)
            .AllowHostAccess();

        group.MapGet("/transactions", ListTransactionsEndpoint.HandleAsync)
            .WithName("ListBalanceTransactions")
            .WithSummary("Returns paginated transaction history for the current tenant.")
            .WithDescription(
                "Returns the append-only transaction ledger for the specified currency. "
                + "Ordered by creation date descending (most recent first). "
                + "Each entry shows the transaction type, amount, source, and optional reference.")
            .Produces<IReadOnlyList<BalanceTransactionResponse>>()
            .RequireAuthorization(CustomerBalancePermissions.Transactions.Read)
            .AllowHostAccess();

        group.MapPost("/balance/credit", AdminCreditEndpoint.HandleAsync)
            .WithName("AddAdminCredit")
            .WithSummary("Adds a manual credit to the tenant's balance.")
            .WithDescription(
                "Creates a credit transaction on the tenant's balance account. "
                + "The account is created automatically if it does not exist for the specified currency. "
                + "Supports optional expiration date for promotional credits.")
            .Produces<CustomerBalanceResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .RequireAuthorization(CustomerBalancePermissions.Credits.Manage)
            .AllowHostAccess();

        group.MapPost("/balance/debit", AdminDebitEndpoint.HandleAsync)
            .WithName("ApplyAdminDebit")
            .WithSummary("Debits the tenant's balance manually (admin tooling).")
            .WithDescription(
                "Creates a ManualAdjustment debit transaction on the tenant's balance account — "
                + "for corrections, scheduled drawdowns, and non-invoice adjustments. "
                + "Returns 404 when no account exists for the (tenant, currency); 422 when the balance "
                + "is insufficient. Idempotent at the application level when the same ReferenceId is "
                + "supplied — replays return the original outcome without double-debiting.")
            .Produces<CustomerBalanceResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .RequireAuthorization(CustomerBalancePermissions.Credits.Manage)
            .AllowHostAccess();

        return group;
    }
}
