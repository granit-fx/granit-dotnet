using Granit.Contacts;
using Granit.Contacts.Domain;
using Granit.Contacts.Domain.ValueObjects;
using Granit.CustomerBalance.Domain;
using Granit.CustomerBalance.Endpoints.Dtos;
using Granit.MultiTenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Granit.CustomerBalance.Endpoints.Internal;

internal static class GetBalanceEndpoint
{
    internal static async Task<Results<Ok<CustomerBalanceResponse>, ProblemHttpResult>> HandleAsync(
        string currency,
        [FromServices] IBalanceAccountReader accountReader,
        [FromServices] IDefaultContactResolver contactResolver,
        [FromServices] ICurrentTenant currentTenant,
        CancellationToken cancellationToken)
    {
        if (!currentTenant.IsAvailable)
        {
            return TypedResults.Problem("Tenant context required.", statusCode: StatusCodes.Status422UnprocessableEntity);
        }

        Contact? contact = await contactResolver
            .GetDefaultForTenantAsync(currentTenant.Id!.Value, cancellationToken)
            .ConfigureAwait(false);

        if (contact is null)
        {
            return TypedResults.Ok(new CustomerBalanceResponse(
                Guid.Empty, currency.ToUpperInvariant(), 0m, null));
        }

        BalanceAccount? account = await accountReader
            .GetByContactAndCurrencyAsync(ContactId.Create(contact.Id), currency, cancellationToken)
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
