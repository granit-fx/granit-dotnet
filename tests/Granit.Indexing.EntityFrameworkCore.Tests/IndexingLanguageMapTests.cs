using Shouldly;
using Xunit;

namespace Granit.Indexing.EntityFrameworkCore.Tests;

public sealed class IndexingLanguageMapTests
{
    [Theory]
    [InlineData("en", "english")]
    [InlineData("fr", "french")]
    [InlineData("ar", "arabic")]
    [InlineData("yi", "yiddish")]
    public void GetPostgresDictionary_maps_known_iso_codes(string iso, string expected) =>
        IndexingLanguageMap.GetPostgresDictionary(iso, "simple").ShouldBe(expected);

    [Fact]
    public void GetPostgresDictionary_is_case_insensitive() =>
        IndexingLanguageMap.GetPostgresDictionary("EN", "simple").ShouldBe("english");

    [Theory]
    [InlineData("zz")]   // unknown ISO code
    [InlineData(null)]   // no language detected
    [InlineData("")]     // empty
    public void GetPostgresDictionary_returns_the_fallback_when_unmapped(string? iso) =>
        IndexingLanguageMap.GetPostgresDictionary(iso, "simple").ShouldBe("simple");

    [Fact]
    public void GetPostgresDictionary_requires_a_non_empty_fallback() =>
        Should.Throw<ArgumentException>(() => IndexingLanguageMap.GetPostgresDictionary("en", ""));

    [Theory]
    [InlineData("en", true)]
    [InlineData("DE", true)]
    [InlineData("zz", false)]
    public void HasMapping_reflects_the_known_dictionary_set(string iso, bool expected) =>
        IndexingLanguageMap.HasMapping(iso).ShouldBe(expected);

    [Fact]
    public void HasMapping_requires_a_non_empty_code() =>
        Should.Throw<ArgumentException>(() => IndexingLanguageMap.HasMapping(""));
}
