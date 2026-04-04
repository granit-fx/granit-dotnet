using System.Collections.Frozen;
using Granit.CustomerBalance.Diagnostics;
using Granit.CustomerBalance.Domain;
using Granit.CustomerBalance.Endpoints.Dtos;
using Granit.Guids;
using Granit.MultiTenancy;
using Granit.Timing;
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
        [FromServices] IBalanceAccountReader accountReader,
        [FromServices] IBalanceAccountWriter accountWriter,
        [FromServices] ICurrentTenant currentTenant,
        [FromServices] IGuidGenerator guidGenerator,
        [FromServices] IClock clock,
        [FromServices] CustomerBalanceMetrics metrics,
        CancellationToken cancellationToken)
    {
        if (!currentTenant.IsAvailable)
        {
            return TypedResults.Problem("Tenant context required.", statusCode: StatusCodes.Status400BadRequest);
        }

        Guid tenantId = currentTenant.Id!.Value;
        string currency = request.Currency.ToUpperInvariant();

        BalanceAccount? account = await accountReader
            .GetByTenantAndCurrencyAsync(tenantId, currency, cancellationToken)
            .ConfigureAwait(false);

        if (account is null)
        {
            account = BalanceAccount.Create(guidGenerator.Create(), tenantId, currency);
            await accountWriter.AddAsync(account, cancellationToken).ConfigureAwait(false);

            account = (await accountReader
                .GetByTenantAndCurrencyAsync(tenantId, currency, cancellationToken)
                .ConfigureAwait(false))!;
        }

        if (!Enum.TryParse<TransactionSource>(request.Source, ignoreCase: true, out TransactionSource source)
            || !AllowedAdminSources.Contains(source))
        {
            return TypedResults.Problem(
                "Invalid or disallowed transaction source. Allowed: Promotional, ManualAdjustment.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        account.Credit(
            request.Amount,
            source,
            request.Reason,
            clock.Now,
            guidGenerator.Create(),
            expiresAt: request.ExpiresAt);

        await accountWriter.UpdateAsync(account, cancellationToken).ConfigureAwait(false);
        metrics.RecordCredited(tenantId.ToString(), currency, source.ToString());

        return TypedResults.Ok(new CustomerBalanceResponse(
            account.Id, account.Currency, account.Balance, account.ModifiedAt));
    }
}
