namespace Granit.Entities.Actions.Execution;

/// <summary>
/// Outcome of a single-entity action execution. Used by
/// <see cref="IEntityActionExecutor{TEntity}.ExecuteAsync"/>.
/// </summary>
public sealed record ActionResult(bool IsSuccess, string? ErrorMessage = null)
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
public sealed record BulkActionResult(int AffectedCount, IReadOnlyList<BulkFailure> Failures)
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
public sealed record BulkFailure(string EntityId, string ErrorMessage);
