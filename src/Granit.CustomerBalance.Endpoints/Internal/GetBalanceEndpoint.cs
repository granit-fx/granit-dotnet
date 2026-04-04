using Granit.CustomerBalance.Domain;
using Granit.CustomerBalance.Endpoints.Dtos;
using Granit.MultiTenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Granit.CustomerBalance.Endpoints.Internal;

internal static class GetBalanceEndpoint
{
    internal static async Task<Results<Ok<CustomerBalanceResponse>, NotFound>> HandleAsync(
        string currency,
        [FromServices] IBalanceAccountReader accountReader,
        [FromServices] ICurrentTenant currentTenant,
        CancellationToken cancellationToken)
    {
        if (!currentTenant.IsAvailable)
        {
            return TypedResults.NotFound();
        }

        BalanceAccount? account = await accountReader
            .GetByTenantAndCurrencyAsync(currentTenant.Id!.Value, currency, cancellationToken)
            .ConfigureAwait(false);

        if (account is null)
        {
            return TypedResults.Ok(new CustomerBalanceResponse(
                Guid.Empty, currency.ToUpperInvariant(), 0m, null));
        }

        return TypedResults.Ok(new CustomerBalanceResponse(
            account.Id, account.Currency, account.Balance, account.ModifiedAt));
    }
}
