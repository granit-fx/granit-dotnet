using Granit.Browsing.Capabilities;
using PuppeteerSharp.PageAccessibility;

namespace Granit.Browsing.PuppeteerSharp.Internal;

/// <summary>
/// PuppeteerSharp implementation of <see cref="IAccessibilityCapability"/>. Wraps the
/// engine's <c>page.Accessibility.SnapshotAsync()</c> output into the provider-neutral
/// <see cref="AccessibilityTreeNode"/> tree.
/// </summary>
internal sealed class PuppeteerAccessibilityCapability : IAccessibilityCapability
{
    /// <inheritdoc/>
    public async Task<AccessibilityTreeNode> SnapshotTreeAsync(IBrowserPage page, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(page);
        if (page is not PuppeteerBrowserPage puppeteerPage)
        {
            throw new InvalidOperationException(
                $"PuppeteerAccessibilityCapability requires a page produced by {nameof(PuppeteerHeadlessBrowser)}; got {page.GetType().Name}.");
        }

        SerializedAXNode? root = await puppeteerPage.UnderlyingPage.Accessibility
            .SnapshotAsync()
            .ConfigureAwait(false);

        return Convert(root);
    }

    private static AccessibilityTreeNode Convert(SerializedAXNode? node)
    {
        if (node is null)
        {
            return new AccessibilityTreeNode(Role: "unknown", Name: null, Description: null, Value: null, Children: []);
        }

        IReadOnlyList<AccessibilityTreeNode> children;
        if (node.Children is { Length: > 0 } puppeteerChildren)
        {
            List<AccessibilityTreeNode> mapped = new(puppeteerChildren.Length);
            foreach (SerializedAXNode child in puppeteerChildren)
            {
                mapped.Add(Convert(child));
            }
            children = mapped;
        }
        else
        {
            children = [];
        }

        return new AccessibilityTreeNode(
            Role: node.Role ?? "unknown",
            Name: node.Name,
            Description: node.Description,
            Value: node.Value,
            Children: children);
    }
}
