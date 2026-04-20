using Granit.Authorization.Extensions;
using Granit.CustomerBalance.Endpoints.Dtos;
using Granit.CustomerBalance.Endpoints.Internal;
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
    public static RouteGroupBuilder MapGranitCustomerBalance(
        this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints
            .MapGranitGroup("customer-balance")
            .WithTags("CustomerBalance");

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

        return group;
    }
}
