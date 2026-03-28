using Granit.Templating.EntityFrameworkCore.Entities;
using Granit.Templating.Keys;
using Granit.Templating.Pipeline;
using Granit.Templating.Store;

namespace Granit.Templating.EntityFrameworkCore.Internal;

/// <summary>
/// <see cref="ITemplateResolver"/> backed by <see cref="IDocumentTemplateStoreReader"/>.
/// Returns the currently published template revision for the requested key.
/// </summary>
/// <remarks>
/// Priority is <c>100</c> — higher than <see cref="Resolvers.EmbeddedTemplateResolver"/> (<c>-100</c>),
/// so store-managed templates take precedence over embedded assembly resources.
/// </remarks>
internal sealed class StoreTemplateResolver(IDocumentTemplateStoreReader storeReader) : ITemplateResolver
{
    /// <inheritdoc/>
    public int Priority => 100;

    /// <inheritdoc/>
    public Task<TemplateDescriptor?> TryResolveAsync(
        TemplateKey key, CancellationToken cancellationToken = default) =>
        storeReader.TryGetPublishedAsync(key, cancellationToken);
}
