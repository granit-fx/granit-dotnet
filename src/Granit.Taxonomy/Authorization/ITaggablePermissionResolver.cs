namespace Granit.Taxonomy.Authorization;

/// <summary>
/// Composite permission gate for tag-assignment operations. Resolves the
/// per-target permission required to assign or unassign a tag on
/// <c>(targetType, targetId)</c> in addition to the base
/// <c>Taxonomy.Tags.Manage</c> permission.
/// </summary>
/// <remarks>
/// Phase T2.2 ships a no-op default. T6.1 tightens the gate for
/// <c>Granit.Documents</c> by requiring <c>Documents.Documents.Manage</c> on the
/// target document. Hosts that taggable other aggregates register their own
/// resolver via DI.
/// </remarks>
public interface ITaggablePermissionResolver
{
    /// <summary>
    /// Returns <c>true</c> when the calling principal is authorised to assign or
    /// unassign tags on the target. The default implementation accepts every call.
    /// </summary>
    Task<bool> IsAuthorizedAsync(
        string targetType,
        Guid targetId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Default no-op resolver — allows every tag-assignment call to pass through. Hosts
/// can replace this in DI to tighten the gate per target type.
/// </summary>
public sealed class AllowAllTaggablePermissionResolver : ITaggablePermissionResolver
{
    /// <inheritdoc />
    public Task<bool> IsAuthorizedAsync(
        string targetType,
        Guid targetId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(true);
}
