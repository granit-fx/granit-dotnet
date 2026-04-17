using System.Text.Json.Nodes;
using Granit.Http.ApiDocumentation;
using Granit.ReferenceData.Endpoints.Dtos;
using Granit.ReferenceData.Endpoints.Internal;
using Shouldly;
using Xunit;

namespace Granit.ReferenceData.Endpoints.Tests;

public sealed class ReferenceDataSchemaExampleProviderTests
{
    private readonly ReferenceDataSchemaExampleProvider _provider = new();

    [Fact]
    public void Implements_ISchemaExampleProvider() => _provider.ShouldBeAssignableTo<ISchemaExampleProvider>();

    [Fact]
    public void GetExamples_ContainsCreateRequestExample()
    {
        IReadOnlyDictionary<Type, JsonNode> examples = _provider.GetExamples();

        examples.ShouldContainKey(typeof(ReferenceDataCreateRequest));
    }

    [Fact]
    public void GetExamples_ContainsUpdateRequestExample()
    {
        IReadOnlyDictionary<Type, JsonNode> examples = _provider.GetExamples();

        examples.ShouldContainKey(typeof(ReferenceDataUpdateRequest));
    }

    [Fact]
    public void GetExamples_CreateRequest_HasCodeProperty()
    {
        IReadOnlyDictionary<Type, JsonNode> examples = _provider.GetExamples();
        JsonNode createExample = examples[typeof(ReferenceDataCreateRequest)];

        createExample["code"]!.GetValue<string>().ShouldBe("EUR");
    }

    [Fact]
    public void GetExamples_CreateRequest_HasLabelEnProperty()
    {
        IReadOnlyDictionary<Type, JsonNode> examples = _provider.GetExamples();
        JsonNode createExample = examples[typeof(ReferenceDataCreateRequest)];

        createExample["labelEn"]!.GetValue<string>().ShouldBe("Europe");
    }

    [Fact]
    public void GetExamples_CreateRequest_HasSortOrderProperty()
    {
        IReadOnlyDictionary<Type, JsonNode> examples = _provider.GetExamples();
        JsonNode createExample = examples[typeof(ReferenceDataCreateRequest)];

        createExample["sortOrder"]!.GetValue<int>().ShouldBe(1);
    }

    [Fact]
    public void GetExamples_UpdateRequest_HasActivatedProperty()
    {
        IReadOnlyDictionary<Type, JsonNode> examples = _provider.GetExamples();
        JsonNode updateExample = examples[typeof(ReferenceDataUpdateRequest)];

        updateExample["activated"]!.GetValue<bool>().ShouldBeTrue();
    }

    [Fact]
    public void GetExamples_UpdateRequest_HasLabelEnProperty()
    {
        IReadOnlyDictionary<Type, JsonNode> examples = _provider.GetExamples();
        JsonNode updateExample = examples[typeof(ReferenceDataUpdateRequest)];

        updateExample["labelEn"]!.GetValue<string>().ShouldBe("Europe");
    }

    [Fact]
    public void GetExamples_Returns_ExactlyTwoEntries()
    {
        IReadOnlyDictionary<Type, JsonNode> examples = _provider.GetExamples();

        examples.Count.ShouldBe(2);
    }
}
