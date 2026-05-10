namespace Granit.Documents.Authorization;

/// <summary>
/// Effective permission level resolved against a folder or a document for a given principal.
/// </summary>
/// <remarks>
/// Distinct from <see cref="Domain.SharePermissionLevel"/> because the resolver must be able
/// to express "no grant matched" as a first-class outcome. Numeric ordering matches the
/// ADR-052 highest-wins rule: <see cref="None"/> &lt; <see cref="Read"/> &lt; <see cref="Edit"/>
/// &lt; <see cref="Manage"/>, so callers may rank with <see cref="System.Linq.Enumerable.Max{TSource}(System.Collections.Generic.IEnumerable{TSource})"/>.
/// </remarks>
public enum EffectivePermissionLevel
{
    /// <summary>No grant matched — the caller has no access through the share ACL.</summary>
    None = 0,

    /// <summary>Download / view metadata.</summary>
    Read = 1,

    /// <summary>Edit metadata, upload new versions, rename, move, trash.</summary>
    Edit = 2,

    /// <summary>Full control, including managing shares on the target.</summary>
    Manage = 3,
}
