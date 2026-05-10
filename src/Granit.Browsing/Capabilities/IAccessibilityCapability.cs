using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Granit.Browsing.Capabilities;

/// <summary>
/// Snapshots the accessibility tree of a rendered page. Available on every supported
/// engine (Chromium, Firefox, WebKit). Useful for accessibility-audit features and
/// screen-reader-driven test harnesses.
/// </summary>
public interface IAccessibilityCapability
{
    /// <summary>Snapshots the accessibility tree of the supplied page.</summary>
    Task<AccessibilityTreeNode> SnapshotTreeAsync(IBrowserPage page, CancellationToken cancellationToken = default);
}

/// <summary>A node in the accessibility tree.</summary>
/// <param name="Role">ARIA role.</param>
/// <param name="Name">Accessible name (computed by the engine).</param>
/// <param name="Description">Accessible description, when available.</param>
/// <param name="Value">Current value for inputs / progress bars / etc.</param>
/// <param name="Children">Child nodes in document order.</param>
public sealed record AccessibilityTreeNode(
    string Role,
    string? Name,
    string? Description,
    string? Value,
    IReadOnlyList<AccessibilityTreeNode> Children);
