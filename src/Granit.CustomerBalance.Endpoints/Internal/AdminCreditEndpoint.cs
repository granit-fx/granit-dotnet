using System.Collections.Frozen;
using Granit.CustomerBalance.Domain;
using Granit.CustomerBalance.Endpoints.Dtos;
using Granit.MultiTenancy;
using Granit.Parties.Domain.ValueObjects;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Granit.CustomerBalance.Endpoints.Internal;

internal static class AdminCreditEndpoint
{
    private static readonly FrozenSet<TransactionSource> AllowedAdminSources =
        FrozenSet.ToFrozenSet([TransactionSource.Promotional, TransactionSource.ManualAdjustment]);

    internal static async Task<Results<Ok<CustomerBalanceResponse>, ProblemHttpResult>> HandleAsync(
        AdminCreditRequest request,
        [FromServices] IAdminCreditService creditService,
        [FromServices] ICurrentTenant currentTenant,
        CancellationToken cancellationToken)
    {
        if (!currentTenant.IsAvailable)
        {
            return TypedResults.Problem("Tenant context required.", statusCode: StatusCodes.Status422UnprocessableEntity);
        }

        if (!Enum.TryParse<TransactionSource>(request.Source, ignoreCase: true, out TransactionSource source)
            || !AllowedAdminSources.Contains(source))
        {
            return TypedResults.Problem(
                "Invalid or disallowed transaction source. Allowed: Promotional, ManualAdjustment.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        Guid tenantId = currentTenant.Id!.Value;
        string currency = request.Currency.ToUpperInvariant();

        BalanceAccount account = await creditService.ApplyAsync(
            tenantId,
            PartyId.Create(request.PartyId),
            request.Amount,
            currency,
            source,
            request.Reason,
            request.ExpiresAt,
            cancellationToken).ConfigureAwait(false);

        return TypedResults.Ok(new CustomerBalanceResponse(
            account.Id, account.Currency, account.Balance, account.ModifiedAt));
    }
}
