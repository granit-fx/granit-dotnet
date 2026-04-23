using System.Globalization;
using Granit.DataLookup.Descriptors;
using Granit.QueryEngine;
using Granit.ReferenceData.Domain;
using Granit.ReferenceData.Lookups;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.ReferenceData.Tests.Lookups;

public sealed class ReferenceDataLookupSourceTests
{
    [Fact]
    public void Name_scope_keys_and_kind_are_exposed()
    {
        IReferenceDataStoreReader<FakeCountry> reader = Substitute.For<IReferenceDataStoreReader<FakeCountry>>();
        ReferenceDataLookupSource<FakeCountry> source = new("ref-fake-country", reader);

        source.Name.ShouldBe("ref-fake-country");
        source.RequiredPermission.ShouldBeNull();
        source.ScopeKeys.ShouldBeEmpty();

        Granit.DataLookup.Registry.IKindProviderLookupSource kindProvider = source;
        kindProvider.Kind.ShouldBe(LookupKind.ReferenceData);
    }

    [Fact]
    public async Task Search_projects_code_and_current_culture_label()
    {
        await UsingCulture("fr", async () =>
        {
            FakeCountry be = new() { Code = "BE", LabelEn = "Belgium", LabelFr = "Belgique" };
            FakeCountry fr = new() { Code = "FR", LabelEn = "France", LabelFr = "France" };

            IReferenceDataStoreReader<FakeCountry> reader = Substitute.For<IReferenceDataStoreReader<FakeCountry>>();
            reader.GetAllAsync(Arg.Any<ReferenceDataQuery?>(), Arg.Any<CancellationToken>())
                .Returns(new PagedResult<FakeCountry>([be, fr], 2, HasMore: false));

            ReferenceDataLookupSource<FakeCountry> source = new("ref-fake-country", reader);

            LookupResult result = await source.SearchAsync(new LookupQuery(), TestContext.Current.CancellationToken);

            result.Items.Select(i => i.Value).ShouldBe(["BE", "FR"]);
            result.Items.Select(i => i.Label).ShouldBe(["Belgique", "France"]);
            result.TotalCount.ShouldBe(2);
        });
    }

    [Fact]
    public async Task Search_in_english_uses_label_en()
    {
        await UsingCulture("en", async () =>
        {
            FakeCountry be = new() { Code = "BE", LabelEn = "Belgium", LabelFr = "Belgique" };

            IReferenceDataStoreReader<FakeCountry> reader = Substitute.For<IReferenceDataStoreReader<FakeCountry>>();
            reader.GetAllAsync(Arg.Any<ReferenceDataQuery?>(), Arg.Any<CancellationToken>())
                .Returns(new PagedResult<FakeCountry>([be], 1, HasMore: false));

            ReferenceDataLookupSource<FakeCountry> source = new("ref-fake-country", reader);

            LookupResult result = await source.SearchAsync(new LookupQuery(), TestContext.Current.CancellationToken);

            result.Items.ShouldHaveSingleItem().Label.ShouldBe("Belgium");
        });
    }

    [Fact]
    public async Task Search_falls_back_to_label_en_when_culture_specific_label_is_empty()
    {
        await UsingCulture("hi", async () =>
        {
            FakeCountry be = new() { Code = "BE", LabelEn = "Belgium", LabelHi = string.Empty };

            IReferenceDataStoreReader<FakeCountry> reader = Substitute.For<IReferenceDataStoreReader<FakeCountry>>();
            reader.GetAllAsync(Arg.Any<ReferenceDataQuery?>(), Arg.Any<CancellationToken>())
                .Returns(new PagedResult<FakeCountry>([be], 1, HasMore: false));

            ReferenceDataLookupSource<FakeCountry> source = new("ref-fake-country", reader);

            LookupResult result = await source.SearchAsync(new LookupQuery(), TestContext.Current.CancellationToken);

            result.Items.ShouldHaveSingleItem().Label.ShouldBe("Belgium");
        });
    }

    [Fact]
    public async Task Search_falls_back_to_code_when_label_en_is_empty()
    {
        await UsingCulture("en", async () =>
        {
            FakeCountry xx = new() { Code = "XX", LabelEn = string.Empty };

            IReferenceDataStoreReader<FakeCountry> reader = Substitute.For<IReferenceDataStoreReader<FakeCountry>>();
            reader.GetAllAsync(Arg.Any<ReferenceDataQuery?>(), Arg.Any<CancellationToken>())
                .Returns(new PagedResult<FakeCountry>([xx], 1, HasMore: false));

            ReferenceDataLookupSource<FakeCountry> source = new("ref-fake-country", reader);

            LookupResult result = await source.SearchAsync(new LookupQuery(), TestContext.Current.CancellationToken);

            result.Items.ShouldHaveSingleItem().Label.ShouldBe("XX");
        });
    }

    [Fact]
    public async Task Search_propagates_search_term_and_paging_to_reader()
    {
        IReferenceDataStoreReader<FakeCountry> reader = Substitute.For<IReferenceDataStoreReader<FakeCountry>>();
        reader.GetAllAsync(Arg.Any<ReferenceDataQuery?>(), Arg.Any<CancellationToken>())
            .Returns(new PagedResult<FakeCountry>([], 0, HasMore: false));

        ReferenceDataLookupSource<FakeCountry> source = new("ref-fake-country", reader);

        await source.SearchAsync(new LookupQuery(Search: "bel", Page: 2, PageSize: 30), TestContext.Current.CancellationToken);

        await reader.Received(1).GetAllAsync(
            Arg.Is<ReferenceDataQuery?>(q =>
                q != null && q.ActiveOnly &&
                q.SearchTerm == "bel" && q.Page == 2 && q.PageSize == 30),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Resolve_returns_item_for_known_code()
    {
        FakeCountry be = new() { Code = "BE", LabelEn = "Belgium" };
        IReferenceDataStoreReader<FakeCountry> reader = Substitute.For<IReferenceDataStoreReader<FakeCountry>>();
        reader.GetByCodeAsync("BE", Arg.Any<CancellationToken>()).Returns(be);

        ReferenceDataLookupSource<FakeCountry> source = new("ref-fake-country", reader);

        LookupItem? item = await source.ResolveByValueAsync("BE", TestContext.Current.CancellationToken);

        item.ShouldNotBeNull();
        item!.Value.ShouldBe("BE");
        item.Label.ShouldBe("Belgium");
    }

    [Fact]
    public async Task Resolve_returns_null_for_unknown_code()
    {
        IReferenceDataStoreReader<FakeCountry> reader = Substitute.For<IReferenceDataStoreReader<FakeCountry>>();
        reader.GetByCodeAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((FakeCountry?)null);

        ReferenceDataLookupSource<FakeCountry> source = new("ref-fake-country", reader);

        LookupItem? item = await source.ResolveByValueAsync("ZZ", TestContext.Current.CancellationToken);

        item.ShouldBeNull();
    }

    [Fact]
    public void Requires_non_null_name_and_reader()
    {
        IReferenceDataStoreReader<FakeCountry> reader = Substitute.For<IReferenceDataStoreReader<FakeCountry>>();

        Should.Throw<ArgumentException>(() => new ReferenceDataLookupSource<FakeCountry>("", reader));
        Should.Throw<ArgumentNullException>(() => new ReferenceDataLookupSource<FakeCountry>("ref-x", null!));
    }

    private static async Task UsingCulture(string cultureName, Func<Task> action)
    {
        CultureInfo previous = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(cultureName);
        try
        {
            await action();
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }
}

// Non-nested (public) so NSubstitute (Castle DynamicProxy) can create a proxy for
// IReferenceDataStoreReader<FakeCountry>.
public sealed class FakeCountry : ReferenceDataEntity;
