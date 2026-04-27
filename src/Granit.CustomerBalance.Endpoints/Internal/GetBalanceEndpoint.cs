using Granit.CustomerBalance.Domain;
using Granit.CustomerBalance.Endpoints.Dtos;
using Granit.MultiTenancy;
using Granit.Parties;
using Granit.Parties.Domain;
using Granit.Parties.Domain.ValueObjects;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Granit.CustomerBalance.Endpoints.Internal;

internal static class GetBalanceEndpoint
{
    internal static async Task<Results<Ok<CustomerBalanceResponse>, ProblemHttpResult>> HandleAsync(
        string currency,
        [FromServices] IBalanceAccountReader accountReader,
        [FromServices] IDefaultPartyResolver contactResolver,
        [FromServices] ICurrentTenant currentTenant,
        CancellationToken cancellationToken)
    {
        if (!currentTenant.IsAvailable)
        {
            return TypedResults.Problem("Tenant context required.", statusCode: StatusCodes.Status422UnprocessableEntity);
        }

        Party? contact = await contactResolver
            .GetDefaultForTenantAsync(currentTenant.Id!.Value, cancellationToken)
            .ConfigureAwait(false);

        if (contact is null)
        {
            return TypedResults.Ok(new CustomerBalanceResponse(
                Guid.Empty, currency.ToUpperInvariant(), 0m, null));
        }

        BalanceAccount? account = await accountReader
            .GetByContactAndCurrencyAsync(PartyId.Create(contact.Id), currency, cancellationToken)
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
