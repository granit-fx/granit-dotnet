namespace Granit.Domain;

/// <summary>
/// Interface for entities that maintain a versioned history of changes.
/// Multiple rows can share the same <see cref="VersionId"/>, each with a different
/// <see cref="Version"/> number.
/// </summary>
/// <remarks>
/// <para>
/// This interface is independent of the workflow system. It can be used alone
/// for pure versioning (audit/history) or combined with <c>IWorkflowStateful</c>
/// for a full versioned publication workflow.
/// </para>
/// <para>
/// The <see cref="Version"/> is auto-incremented by <c>VersioningInterceptor</c>
/// on <c>EntityState.Added</c>. If <see cref="VersionId"/> is <see cref="Guid.Empty"/>
/// at insert time, a new identifier is generated automatically.
/// </para>
/// </remarks>
public interface IVersioned
{
    /// <summary>
    /// Stable identifier shared across all versions of this logical entity.
    /// All rows with the same <see cref="VersionId"/> represent different versions
    /// of the same business object.
    /// </summary>
    Guid VersionId { get; set; }

    /// <summary>
    /// Monotonically increasing version number (1-based).
    /// Assigned automatically by <c>VersioningInterceptor</c> on insert.
    /// </summary>
    int Version { get; set; }
}
