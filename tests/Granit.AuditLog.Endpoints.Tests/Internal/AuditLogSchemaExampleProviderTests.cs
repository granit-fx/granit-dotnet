using System.Text.Json.Nodes;
using Granit.AuditLog.Endpoints.Dtos;
using Granit.AuditLog.Endpoints.Internal;
using Shouldly;
using Xunit;

namespace Granit.AuditLog.Endpoints.Tests.Internal;

public sealed class AuditLogSchemaExampleProviderTests
{
    [Fact]
    public void GetExamples_ContainsEntryResponseExample()
    {
        AuditLogSchemaExampleProvider provider = new();

        IReadOnlyDictionary<Type, JsonNode> examples = provider.GetExamples();

        examples.ShouldContainKey(typeof(AuditLogEntryResponse));
    }

    [Fact]
    public void GetExamples_ContainsDetailResponseExample()
    {
        AuditLogSchemaExampleProvider provider = new();

        IReadOnlyDictionary<Type, JsonNode> examples = provider.GetExamples();

        examples.ShouldContainKey(typeof(AuditLogEntryDetailResponse));
    }

    [Fact]
    public void GetExamples_EntryResponse_HasExpectedFields()
    {
        AuditLogSchemaExampleProvider provider = new();
        IReadOnlyDictionary<Type, JsonNode> examples = provider.GetExamples();

        JsonNode entryExample = examples[typeof(AuditLogEntryResponse)];
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
        AuditLogSchemaExampleProvider provider = new();
        IReadOnlyDictionary<Type, JsonNode> examples = provider.GetExamples();

        JsonNode detailExample = examples[typeof(AuditLogEntryDetailResponse)];
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
        AuditLogSchemaExampleProvider provider = new();

        IReadOnlyDictionary<Type, JsonNode> examples = provider.GetExamples();

        examples.Count.ShouldBe(2);
    }
}
