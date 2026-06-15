using Granit.Domain;
using Granit.QueryEngine.Filtering;
using Shouldly;
using Xunit;

namespace Granit.QueryEngine.Abstractions.Tests;

// Guards against the QueryEngine SingleValueObject<string> search blind spot (issue #2767):
// a value-object column cannot be substring-searched (EF Core cannot translate LIKE over a
// ValueConverter), so the builder must reject it at definition time instead of registering a
// silent no-op. Also covers the opaque error previously raised by `.Value` drill-in selectors.
public sealed class QueryDefinitionBuilderValueObjectGuardTests
{
    [Fact]
    public void GlobalSearch_on_value_object_column_throws()
    {
        QueryDefinitionBuilder<SampleEntity> builder = new();

        ArgumentException ex = Should.Throw<ArgumentException>(
            () => builder.GlobalSearch(e => e.Slug));

        ex.Message.ShouldContain(nameof(SampleSlug));
        ex.Message.ShouldContain("#2767");
    }

    [Fact]
    public void GlobalSearch_on_string_column_succeeds()
    {
        QueryDefinitionBuilder<SampleEntity> builder = new();

        builder.GlobalSearch(e => e.Name);

        builder.GlobalSearchProperties.ShouldBe([nameof(SampleEntity.Name)]);
    }

    [Fact]
    public void GlobalSearch_on_value_object_Value_drill_in_throws_with_hint()
    {
        QueryDefinitionBuilder<SampleEntity> builder = new();

        ArgumentException ex = Should.Throw<ArgumentException>(
            () => builder.GlobalSearch(e => e.Slug.Value));

        ex.Message.ShouldContain("#2767");
        ex.Message.ShouldContain(nameof(SampleEntity.Slug));
    }

    [Fact]
    public void Column_on_value_object_Value_drill_in_throws_with_hint()
    {
        QueryDefinitionBuilder<SampleEntity> builder = new();

        ArgumentException ex = Should.Throw<ArgumentException>(
            () => builder.Column(e => e.Slug.Value));

        ex.Message.ShouldContain("#2767");
    }

    [Fact]
    public void Column_on_whole_value_object_is_allowed()
    {
        QueryDefinitionBuilder<SampleEntity> builder = new();

        builder.Column(e => e.Slug, c => c.Sortable());

        builder.Columns.ShouldContain(c => c.PropertyName == nameof(SampleEntity.Slug));
    }

    [Fact]
    public void AllowGroupBy_on_value_object_throws()
    {
        QueryDefinitionBuilder<SampleEntity> builder = new();

        ArgumentException ex = Should.Throw<ArgumentException>(() => builder.AllowGroupBy(e => e.Slug));
        ex.Message.ShouldContain("#2767");
        ex.Message.ShouldContain(nameof(SampleEntity.Slug));
    }

    [Fact]
    public void Aggregate_on_value_object_throws()
    {
        QueryDefinitionBuilder<SampleEntity> builder = new();

        ArgumentException ex = Should.Throw<ArgumentException>(
            () => builder.Aggregate(e => e.Slug, AggregateFunction.Count, "slugCount"));
        ex.Message.ShouldContain("#2767");
    }

    [Fact]
    public void SupportsCursorPagination_on_value_object_throws()
    {
        QueryDefinitionBuilder<SampleEntity> builder = new();

        ArgumentException ex = Should.Throw<ArgumentException>(
            () => builder.SupportsCursorPagination(e => e.Slug));
        ex.Message.ShouldContain("#2767");
    }

    [Fact]
    public void AllowGroupBy_on_scalar_column_succeeds()
    {
        QueryDefinitionBuilder<SampleEntity> builder = new();

        builder.AllowGroupBy(e => e.Name);

        builder.GroupByFields.ShouldContain(g => g.PropertyName == nameof(SampleEntity.Name));
    }

    private sealed class SampleEntity
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public SampleSlug Slug { get; set; } = null!;
    }

    private sealed class SampleSlug : SingleValueObject<string>
    {
        public override required string Value { get; init; }
        public static SampleSlug Create(string value) => new() { Value = value };
        public static implicit operator string(SampleSlug slug) => slug.Value;
    }
}
