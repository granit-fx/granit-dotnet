using Granit.CustomerBalance.Domain;
using Granit.CustomerBalance.Domain.ValueObjects;
using Granit.CustomerBalance.Endpoints.Dtos;
using Granit.MultiTenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Granit.CustomerBalance.Endpoints.Internal;

internal static class ListTransactionsEndpoint
{
    internal static async Task<Results<Ok<IReadOnlyList<BalanceTransactionResponse>>, ProblemHttpResult>> HandleAsync(
        string currency,
        int page,
        int pageSize,
        [FromServices] IBalanceAccountReader accountReader,
        [FromServices] IBalanceTransactionReader transactionReader,
        [FromServices] ICurrentTenant currentTenant,
        CancellationToken cancellationToken)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        if (!currentTenant.IsAvailable)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status404NotFound);
        }

        BalanceAccount? account = await accountReader
            .GetByTenantAndCurrencyAsync(currentTenant.Id!.Value, currency, cancellationToken)
            .ConfigureAwait(false);

        if (account is null)
        {
            return TypedResults.Ok<IReadOnlyList<BalanceTransactionResponse>>([]);
        }

        IReadOnlyList<BalanceTransaction> transactions = await transactionReader
            .GetByAccountAsync(account.Id, page, pageSize, cancellationToken)
            .ConfigureAwait(false);

        IReadOnlyList<BalanceTransactionResponse> response = transactions
            .Select(t => new BalanceTransactionResponse(
                t.Id,
                t.Type.ToString(),
                t.Amount,
                t.Source.ToString(),
                t.Reason,
                t.ReferenceId,
                t.ReferenceType,
                t.ExpiresAt,
                t.CreatedAt))
            .ToList();

        return TypedResults.Ok(response);
    }
}
