using System.Diagnostics;
using Granit.CustomerBalance.Diagnostics;
using Granit.CustomerBalance.Domain;
using Granit.Guids;
using Granit.Parties.Domain.ValueObjects;
using Granit.Timing;
using Microsoft.Extensions.Logging;

namespace Granit.CustomerBalance.Internal;

/// <summary>
/// Default implementation of <see cref="IAdminCreditService"/>.
/// Applies admin credits to the tenant's balance account.
/// Creates the account if it does not exist for the (TenantId, Currency) pair.
/// </summary>
internal sealed partial class DefaultAdminCreditService(
    IBalanceAccountReader accountReader,
    IBalanceAccountWriter accountWriter,
    IGuidGenerator guidGenerator,
    IClock clock,
    CustomerBalanceMetrics metrics,
    ILogger<DefaultAdminCreditService> logger) : IAdminCreditService
{
    public async Task<BalanceAccount> ApplyAsync(
        Guid tenantId,
        PartyId contactId,
        decimal amount,
        string currency,
        TransactionSource source,
        string reason,
        DateTimeOffset? expiresAt,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(contactId);

        using Activity? activity = CustomerBalanceActivitySource.Source
            .StartActivity(CustomerBalanceActivitySource.CreditBalance);

        BalanceAccount? account = await accountReader
            .GetByContactAndCurrencyAsync(contactId, currency, cancellationToken)
            .ConfigureAwait(false);

        if (account is null)
        {
            account = BalanceAccount.Create(guidGenerator.Create(), tenantId, contactId, currency);
            await accountWriter.AddAsync(account, cancellationToken).ConfigureAwait(false);
            Log.AccountCreated(logger, contactId.Value, currency);

            // Reload to get tracked entity with transactions collection.
            account = (await accountReader
                .GetByContactAndCurrencyAsync(contactId, currency, cancellationToken)
                .ConfigureAwait(false))!;
        }

        account.Credit(
            amount,
            source,
            reason,
            clock.Now,
            guidGenerator.Create(),
            expiresAt: expiresAt);

        await accountWriter.UpdateAsync(account, cancellationToken).ConfigureAwait(false);
        metrics.RecordCredited(tenantId.ToString(), currency, source.ToString());
        Log.AdminCredited(logger, source, amount, account.Balance);

        return account;
    }

    private static partial class Log
    {
        [LoggerMessage(Level = LogLevel.Information, Message = "Admin credit ({Source}) of {Amount} applied, new balance: {NewBalance}")]
        public static partial void AdminCredited(ILogger logger, TransactionSource source, decimal amount, decimal newBalance);

        [LoggerMessage(Level = LogLevel.Information, Message = "Created balance account for contact {PartyId} ({Currency})")]
        public static partial void AccountCreated(ILogger logger, Guid partyId, string currency);
    }
}
