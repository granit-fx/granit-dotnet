using Granit.Exceptions;

namespace Granit.CustomerBalance.Exceptions;

/// <summary>
/// Thrown when a debit operation exceeds the available balance.
/// Maps to <c>400 Bad Request</c> via <see cref="BusinessException"/>.
/// </summary>
public sealed class InsufficientBalanceException : BusinessException
{
    /// <summary>Error code for localization and client-side handling.</summary>
    public const string Code = "CustomerBalance:InsufficientBalance";

    public InsufficientBalanceException(
        Guid accountId, string currency, decimal availableBalance, decimal requestedAmount)
        : base(Code, $"Insufficient balance ({currency}): available {availableBalance}, requested {requestedAmount}.")
    {
        AccountId = accountId;
        Currency = currency;
        AvailableBalance = availableBalance;
        RequestedAmount = requestedAmount;
    }

    /// <summary>The account that had insufficient funds.</summary>
    public Guid AccountId { get; }

    /// <summary>Currency of the account.</summary>
    public string Currency { get; }

    /// <summary>Available balance at the time of the debit attempt.</summary>
    public decimal AvailableBalance { get; }

    /// <summary>Amount that was requested.</summary>
    public decimal RequestedAmount { get; }
}
