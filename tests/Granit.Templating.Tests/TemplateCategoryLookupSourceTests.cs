using Granit.DataLookup.Descriptors;
using Granit.Templating.Internal;
using Granit.Templating.Store;
using Shouldly;
using Xunit;

namespace Granit.Templating.Tests;

public sealed class TemplateCategoryLookupSourceTests
{
    private static readonly Guid Marketing = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Billing = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private sealed class StubReader(params TemplateCategory[] categories) : ITemplateCategoryStoreReader
    {
        public Task<IReadOnlyList<TemplateCategory>> ListCategoriesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<TemplateCategory>>(categories);

        public Task<TemplateCategory?> GetCategoryAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(Array.Find(categories, c => c.Id == id));
    }

    private static TemplateCategory Category(Guid id, string name, int sortOrder = 0) =>
        new() { Id = id, Name = name, SortOrder = sortOrder, TemplateCount = 0 };

    private static TemplateCategoryLookupSource Build(params TemplateCategory[] categories) =>
        new(new StubReader(categories));

    [Fact]
    public void Name_and_permission()
    {
        TemplateCategoryLookupSource source = Build();
        source.Name.ShouldBe("template-categories");
        source.RequiredPermission.ShouldBe("Templating.Categories.Read");
    }

    [Fact]
    public async Task Search_returns_all_categories_mapped_to_id_and_name()
    {
        TemplateCategoryLookupSource source = Build(Category(Marketing, "Marketing"), Category(Billing, "Billing"));

        LookupResult result = await source.SearchAsync(new LookupQuery(PageSize: 8), TestContext.Current.CancellationToken);

        result.Items.Select(i => i.Value).ShouldBe([Marketing, Billing]);
        result.Items.Select(i => i.Label).ShouldBe(["Marketing", "Billing"]);
    }

    [Fact]
    public async Task Search_filters_by_name_case_insensitively()
    {
        TemplateCategoryLookupSource source = Build(Category(Marketing, "Marketing"), Category(Billing, "Billing"));

        LookupResult result = await source.SearchAsync(new LookupQuery(Search: "bill", PageSize: 8), TestContext.Current.CancellationToken);

        result.Items.ShouldHaveSingleItem();
        result.Items[0].Value.ShouldBe(Billing);
    }

    [Fact]
    public async Task Search_caps_at_page_size()
    {
        TemplateCategoryLookupSource source = Build(Category(Marketing, "Marketing"), Category(Billing, "Billing"));

        LookupResult result = await source.SearchAsync(new LookupQuery(PageSize: 1), TestContext.Current.CancellationToken);

        result.Items.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Resolve_returns_the_category_by_id()
    {
        TemplateCategoryLookupSource source = Build(Category(Marketing, "Marketing"));

        LookupItem? item = await source.ResolveByValueAsync(Marketing.ToString(), TestContext.Current.CancellationToken);

        item.ShouldNotBeNull();
        item.Value.ShouldBe(Marketing);
        item.Label.ShouldBe("Marketing");
    }

    [Fact]
    public async Task Resolve_returns_null_for_unknown_or_malformed_value()
    {
        TemplateCategoryLookupSource source = Build(Category(Marketing, "Marketing"));

        (await source.ResolveByValueAsync(Billing.ToString(), TestContext.Current.CancellationToken)).ShouldBeNull();
        (await source.ResolveByValueAsync("not-a-guid", TestContext.Current.CancellationToken)).ShouldBeNull();
    }
}
