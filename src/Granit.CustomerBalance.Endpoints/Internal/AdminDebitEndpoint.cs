using Granit.CustomerBalance.Domain;
using Granit.CustomerBalance.Endpoints.Dtos;
using Granit.CustomerBalance.Exceptions;
using Granit.MultiTenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Granit.CustomerBalance.Endpoints.Internal;

internal static class AdminDebitEndpoint
{
    internal static async Task<Results<Ok<CustomerBalanceResponse>, ProblemHttpResult>> HandleAsync(
        AdminDebitRequest request,
        [FromServices] IAdminDebitService debitService,
        [FromServices] ICurrentTenant currentTenant,
        CancellationToken cancellationToken)
    {
        if (!currentTenant.IsAvailable)
        {
            return TypedResults.Problem("Tenant context required.", statusCode: StatusCodes.Status422UnprocessableEntity);
        }

        Guid tenantId = currentTenant.Id!.Value;
        string currency = request.Currency.ToUpperInvariant();

        try
        {
            BalanceAccount account = await debitService
                .DebitAsync(
                    tenantId,
                    request.Amount,
                    currency,
                    request.Reason,
                    request.ReferenceId,
                    request.ReferenceType,
                    cancellationToken)
                .ConfigureAwait(false);

            return TypedResults.Ok(new CustomerBalanceResponse(
                account.Id, account.Currency, account.Balance, account.ModifiedAt));
        }
        catch (InsufficientBalanceException ex)
        {
            return TypedResults.Problem(
                detail: ex.Message,
                title: "Granit:CustomerBalance:InsufficientBalance",
                statusCode: StatusCodes.Status422UnprocessableEntity);
        }
        catch (InvalidOperationException ex)
        {
            // No account exists for the (tenant, currency) — distinct from insufficient balance.
            return TypedResults.Problem(
                detail: ex.Message,
                title: "Granit:CustomerBalance:AccountNotFound",
                statusCode: StatusCodes.Status404NotFound);
        }
    }
}
