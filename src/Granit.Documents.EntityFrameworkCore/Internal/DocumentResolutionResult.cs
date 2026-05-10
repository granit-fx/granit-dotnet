using Granit.Documents.Authorization;

namespace Granit.Documents.EntityFrameworkCore.Internal;

/// <summary>
/// Internal result shape returned by <see cref="EffectivePermissionResolver.ResolveDocumentAsync"/>
/// — carries the resolved permission level plus the ancestor folder ids visited during
/// resolution. The cache decorator (F6.3) tags the cached entry with each ancestor id so a
/// folder share change invalidates exactly the entries that depended on it.
/// </summary>
internal readonly record struct DocumentResolutionResult(
    EffectivePermissionLevel Permission,
    IReadOnlyList<Guid> AncestorFolderIds);
