namespace Granit.Entities.Actions.Execution;

/// <summary>
/// Outcome of a single-entity action execution. Used by
/// <see cref="IEntityActionExecutor{TEntity}.ExecuteAsync"/>.
/// </summary>
public sealed record ActionResult(
    /// <summary>
    /// Indicates whether the action succeeded. When <see langword="false"/>,
    /// the <see cref="ErrorMessage"/> field contains the reason.
    /// </summary>
    bool IsSuccess,
    /// <summary>
    /// Localization key or plain message explaining the failure.
    /// Populated only when <see cref="IsSuccess"/> is <see langword="false"/>.
    /// </summary>
    string? ErrorMessage = null)
{
    /// <summary>Factory method for successful execution.</summary>
    public static ActionResult Success() => new(IsSuccess: true);

    /// <summary>
    /// Factory method for failed execution with a localization key
    /// (preferred for multi-language UX).
    /// </summary>
    public static ActionResult Failure(string errorLocalizationKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(errorLocalizationKey);
        return new(IsSuccess: false, ErrorMessage: errorLocalizationKey);
    }
}

/// <summary>
/// Outcome of a bulk action execution. Used by
/// <see cref="IBulkActionExecutor{TEntity}.ExecuteBulkAsync"/>.
/// </summary>
public sealed record BulkActionResult(
    /// <summary>
    /// Count of successfully affected rows. Rows that failed are not included.
    /// </summary>
    int AffectedCount,
    /// <summary>
    /// Per-row failure details. When empty, all rows succeeded
    /// (<see cref="AffectedCount"/> equals the batch size).
    /// </summary>
    IReadOnlyList<BulkFailure> Failures)
{
    /// <summary>Factory for full success — no failures.</summary>
    public static BulkActionResult Success(int affectedCount) => new(affectedCount, []);

    /// <summary>Factory for partial or total failure.</summary>
    public static BulkActionResult WithFailures(int affectedCount, params BulkFailure[] failures)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(affectedCount, 0);
        ArgumentNullException.ThrowIfNull(failures);
        return new(affectedCount, failures);
    }
}

/// <summary>
/// Per-row failure detail in a bulk operation. Allows fine-grained error
/// reporting — some rows succeed, some fail, and clients see which ones
/// and why.
/// </summary>
public sealed record BulkFailure(
    /// <summary>
    /// Stable identifier of the entity that failed (typically its primary key
    /// as a GUID or int rendered as a string).
    /// </summary>
    string EntityId,
    /// <summary>
    /// Localization key or plain message explaining why this entity's action
    /// failed (e.g., "Granit:Validation:InsufficientBalance").
    /// </summary>
    string ErrorMessage);
