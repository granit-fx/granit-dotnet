using Granit.DataLookup.Descriptors;
using Granit.DataLookup.Sources;
using Granit.Templating.Store;

namespace Granit.Templating.Internal;

/// <summary>
/// Exposes template categories as the <c>template-categories</c> <see cref="ILookupSource"/> so a
/// <c>CategoryId</c> column renders a typeahead picker (and rehydrates a stored id into the category
/// name). Backed by <see cref="ITemplateCategoryStoreReader"/> with in-memory filtering — categories
/// are a small, bounded set — and gated on <c>Templating.Categories.Read</c>.
/// </summary>
internal sealed class TemplateCategoryLookupSource(ITemplateCategoryStoreReader reader) : ILookupSource
{
    public string Name => "template-categories";

    public string? RequiredPermission => "Templating.Categories.Read";

    public IReadOnlyList<string> ScopeKeys => [];

    public async ValueTask<LookupResult> SearchAsync(LookupQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (query.PageSize <= 0)
        {
            return new LookupResult([]);
        }

        // Already ordered by SortOrder then Name by the reader.
        IReadOnlyList<TemplateCategory> categories = await reader.ListCategoriesAsync(cancellationToken).ConfigureAwait(false);

        IEnumerable<TemplateCategory> matches = string.IsNullOrWhiteSpace(query.Search)
            ? categories
            : categories.Where(c => c.Name.Contains(query.Search, StringComparison.OrdinalIgnoreCase));

        List<LookupItem> items = [.. matches.Take(query.PageSize).Select(ToItem)];
        return new LookupResult(items);
    }

    public async ValueTask<LookupItem?> ResolveByValueAsync(object value, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (!Guid.TryParse(value.ToString(), out Guid id))
        {
            return null;
        }

        TemplateCategory? category = await reader.GetCategoryAsync(id, cancellationToken).ConfigureAwait(false);
        return category is null ? null : ToItem(category);
    }

    private static LookupItem ToItem(TemplateCategory category) => new(category.Id, category.Name);
}
