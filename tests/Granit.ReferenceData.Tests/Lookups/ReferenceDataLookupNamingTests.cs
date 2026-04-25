using Granit.ReferenceData.Lookups;
using Shouldly;
using Xunit;

namespace Granit.ReferenceData.Tests.Lookups;

public sealed class ReferenceDataLookupNamingTests
{
    [Theory]
    [InlineData(typeof(SingleWordEntity), "ref-single-word-entity")]
    [InlineData(typeof(Country), "ref-country")]
    [InlineData(typeof(ProductCategoryXml), "ref-product-category-xml")]
    public void ForEntity_produces_kebab_case_with_ref_prefix(Type type, string expected) =>
        ReferenceDataLookupNaming.ForEntity(type).ShouldBe(expected);

    [Theory]
    [InlineData("Countries", "ref-countries")]
    [InlineData("DocumentTypes", "ref-document-types")]
    [InlineData("IBAN", "ref-iban")]          // all-caps acronym stays glued
    [InlineData("HTTPClient", "ref-http-client")] // acronym followed by Pascal-cased word
    public void ForTypeName_produces_kebab_case(string typeName, string expected) =>
        ReferenceDataLookupNaming.ForTypeName(typeName).ShouldBe(expected);

    [Fact]
    public void ForEntity_throws_on_null() =>
        Should.Throw<ArgumentNullException>(() => ReferenceDataLookupNaming.ForEntity(null!));

    [Fact]
    public void ForTypeName_throws_on_blank() =>
        Should.Throw<ArgumentException>(() => ReferenceDataLookupNaming.ForTypeName(""));

    private sealed class SingleWordEntity;
    private sealed class Country;
    private sealed class ProductCategoryXml;
}
