using Granit.DataLookup.Descriptors;
using Granit.DataLookup.Sources;
using Microsoft.Extensions.Localization;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.DataLookup.Tests.Sources;

public sealed class EnumLookupSourceTests
{
    public enum AggregationType
    {
        Sum,
        Count,
        Max,
    }

    [Fact]
    public void Name_and_scope_keys_are_exposed()
    {
        EnumLookupSource<AggregationType> source = BuildSource("enum-aggregation-type");

        source.Name.ShouldBe("enum-aggregation-type");
        source.ScopeKeys.ShouldBeEmpty();
    }

    [Fact]
    public async Task Search_returns_all_values_when_no_search_term()
    {
        EnumLookupSource<AggregationType> source = BuildSource("enum-aggregation-type");

        LookupResult result = await source.SearchAsync(new LookupQuery(), TestContext.Current.CancellationToken);

        result.Items.Select(i => i.Value).ShouldBe(["Sum", "Count", "Max"]);
        result.TotalCount.ShouldBe(3);
    }

    [Fact]
    public async Task Search_filters_by_label()
    {
        EnumLookupSource<AggregationType> source = BuildSource(
            "enum-aggregation-type",
            labels: new Dictionary<string, string>
            {
                ["Enum:AggregationType.Sum"] = "Sum",
                ["Enum:AggregationType.Count"] = "Count",
                ["Enum:AggregationType.Max"] = "Max",
            });

        LookupResult result = await source.SearchAsync(
            new LookupQuery(Search: "cou"),
            TestContext.Current.CancellationToken);

        result.Items.ShouldHaveSingleItem().Value.ShouldBe("Count");
    }

    [Fact]
    public async Task Search_uses_localized_label_when_available()
    {
        EnumLookupSource<AggregationType> source = BuildSource(
            "enum-aggregation-type",
            labels: new Dictionary<string, string>
            {
                ["Enum:AggregationType.Sum"] = "Somme",
                ["Enum:AggregationType.Count"] = "Compte",
                ["Enum:AggregationType.Max"] = "Maximum",
            });

        LookupResult result = await source.SearchAsync(new LookupQuery(), TestContext.Current.CancellationToken);

        result.Items.Select(i => i.Label).ShouldBe(["Somme", "Compte", "Maximum"]);
    }

    [Fact]
    public async Task Search_falls_back_to_enum_name_when_label_missing()
    {
        EnumLookupSource<AggregationType> source = BuildSource("enum-aggregation-type", labels: []);

        LookupResult result = await source.SearchAsync(new LookupQuery(), TestContext.Current.CancellationToken);

        result.Items.Select(i => i.Label).ShouldBe(["Sum", "Count", "Max"]);
    }

    [Fact]
    public async Task Resolve_returns_item_for_known_value()
    {
        EnumLookupSource<AggregationType> source = BuildSource("enum-aggregation-type");

        LookupItem? item = await source.ResolveByValueAsync("Sum", TestContext.Current.CancellationToken);

        item.ShouldNotBeNull();
        item!.Value.ShouldBe("Sum");
    }

    [Fact]
    public async Task Resolve_returns_null_for_unknown_value()
    {
        EnumLookupSource<AggregationType> source = BuildSource("enum-aggregation-type");

        LookupItem? item = await source.ResolveByValueAsync("Unknown", TestContext.Current.CancellationToken);

        item.ShouldBeNull();
    }

    private static EnumLookupSource<AggregationType> BuildSource(
        string name,
        Dictionary<string, string>? labels = null)
    {
        IStringLocalizer localizer = Substitute.For<IStringLocalizer>();
        localizer[Arg.Any<string>()].Returns(call =>
        {
            string key = (string)call.Args()[0];
            if (labels is not null && labels.TryGetValue(key, out string? value))
            {
                return new LocalizedString(key, value, resourceNotFound: false);
            }

            return new LocalizedString(key, key, resourceNotFound: true);
        });

        return new EnumLookupSource<AggregationType>(name, localizer);
    }
}
