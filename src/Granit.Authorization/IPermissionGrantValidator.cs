namespace Granit.Authorization;

/// <summary>
/// Validates a permission grant before it is written to the store.
/// Multiple validators compose via <c>TryAddEnumerable</c> + fail-fast evaluation.
/// </summary>
/// <remarks>
/// Built-in validators rejected can be augmented by consumers (e.g. forbid granting a
/// permission to a role that does not exist in the current identity provider).
/// </remarks>
public interface IPermissionGrantValidator
{
    /// <summary>
    /// Validates the grant. Return <see cref="PermissionGrantValidationResult.Success"/> to
    /// accept, or <see cref="PermissionGrantValidationResult.Reject"/> with a machine-readable
    /// reason code to refuse.
    /// </summary>
    ValueTask<PermissionGrantValidationResult> ValidateAsync(
        PermissionGrantValidationContext context,
        CancellationToken cancellationToken = default);
}
