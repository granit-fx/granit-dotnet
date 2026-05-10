namespace Granit.Documents.Domain;

/// <summary>
/// Permission level conferred by a <see cref="DocumentShare"/>.
/// </summary>
/// <remarks>
/// Effective permission across overlapping grants is the highest level, ordered
/// <see cref="Read"/> &lt; <see cref="Edit"/> &lt; <see cref="Manage"/>. Phase 1 has no
/// deny-override semantics — see ADR-052 §Permission resolution model.
/// </remarks>
public enum SharePermissionLevel
{
    /// <summary>Download / view metadata.</summary>
    Read = 0,

    /// <summary>Edit metadata, upload new versions, rename, move, trash.</summary>
    Edit = 1,

    /// <summary>Full control, including managing shares on the target.</summary>
    Manage = 2,
}
