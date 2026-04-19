using System.Text.Json.Nodes;
using Granit.Auditing.Dtos;
using Granit.Auditing.Endpoints.Dtos;
using Granit.Auditing.Endpoints.Internal;
using Shouldly;
using Xunit;

namespace Granit.Auditing.Endpoints.Tests.Internal;

public sealed class AuditingSchemaExampleProviderTests
{
    [Fact]
    public void GetExamples_ContainsEntryResponseExample()
    {
        AuditingSchemaExampleProvider provider = new();

        IReadOnlyDictionary<Type, JsonNode> examples = provider.GetExamples();

        examples.ShouldContainKey(typeof(AuditEntrySummary));
    }

    [Fact]
    public void GetExamples_ContainsDetailResponseExample()
    {
        AuditingSchemaExampleProvider provider = new();

        IReadOnlyDictionary<Type, JsonNode> examples = provider.GetExamples();

        examples.ShouldContainKey(typeof(AuditEntryDetailResponse));
    }

    [Fact]
    public void GetExamples_EntryResponse_HasExpectedFields()
    {
        AuditingSchemaExampleProvider provider = new();
        IReadOnlyDictionary<Type, JsonNode> examples = provider.GetExamples();

        JsonNode entryExample = examples[typeof(AuditEntrySummary)];
        JsonObject obj = entryExample.AsObject();

        obj["id"].ShouldNotBeNull();
        obj["timestamp"].ShouldNotBeNull();
        obj["userId"].ShouldNotBeNull();
        obj["userName"].ShouldNotBeNull();
        obj["category"].ShouldNotBeNull();
        obj["entityChangeCount"].ShouldNotBeNull();
    }

    [Fact]
    public void GetExamples_DetailResponse_HasEntityChangesArray()
    {
        AuditingSchemaExampleProvider provider = new();
        IReadOnlyDictionary<Type, JsonNode> examples = provider.GetExamples();

        JsonNode detailExample = examples[typeof(AuditEntryDetailResponse)];
        JsonObject obj = detailExample.AsObject();

        JsonArray entityChanges = obj["entityChanges"]!.AsArray();
        entityChanges.ShouldNotBeNull();
        entityChanges.Count.ShouldBeGreaterThan(0);

        JsonObject firstChange = entityChanges[0]!.AsObject();
        firstChange["entityType"].ShouldNotBeNull();
        firstChange["entityId"].ShouldNotBeNull();
        firstChange["changeType"].ShouldNotBeNull();
        firstChange["propertyChanges"].ShouldNotBeNull();
    }

    [Fact]
    public void GetExamples_ReturnsTwoExamples()
    {
        AuditingSchemaExampleProvider provider = new();

        IReadOnlyDictionary<Type, JsonNode> examples = provider.GetExamples();

        examples.Count.ShouldBe(2);
    }
}
