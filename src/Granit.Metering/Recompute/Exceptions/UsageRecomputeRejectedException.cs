namespace Granit.Metering.Recompute.Exceptions;

/// <summary>
/// Raised when a recompute is rejected at the domain layer (unknown meter,
/// archived meter, invalid window).
/// </summary>
public sealed class UsageRecomputeRejectedException(string reasonCode, string message)
    : Exception(message)
{
    /// <summary>Stable code identifying the rejection reason for HTTP mapping.</summary>
    public string ReasonCode { get; } = reasonCode;
}
