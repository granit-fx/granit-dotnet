namespace Granit.Authorization;

/// <summary>
/// Outcome of an <see cref="IPermissionGrantValidator"/> evaluation.
/// </summary>
public readonly record struct PermissionGrantValidationResult(
    bool IsValid,
    string? ReasonCode,
    string? ReasonMessage)
{
    /// <summary>Accept the grant.</summary>
    public static PermissionGrantValidationResult Success { get; } = new(true, null, null);

    /// <summary>
    /// Reject the grant with a machine-readable <paramref name="reasonCode"/> and an optional
    /// human-readable <paramref name="reasonMessage"/> (server-side logs, not exposed to the client).
    /// </summary>
    public static PermissionGrantValidationResult Reject(string reasonCode, string? reasonMessage = null) =>
        new(false, reasonCode, reasonMessage);
}
