using System.Text.Json.Nodes;
using Granit.Timeline.Endpoints.Dtos;
using Granit.Timeline.Endpoints.Internal;
using Shouldly;
using Xunit;

namespace Granit.Timeline.Endpoints.Tests;

public sealed class TimelineSchemaExampleProviderTests
{
    [Fact]
    public void GetExamples_ContainsPostTimelineEntryRequest()
    {
        TimelineSchemaExampleProvider provider = new();

        IReadOnlyDictionary<Type, JsonNode> examples = provider.GetExamples();

        examples.ShouldContainKey(typeof(PostTimelineEntryRequest));
    }

    [Fact]
    public void GetExamples_PostTimelineEntryRequest_HasEntryType()
    {
        TimelineSchemaExampleProvider provider = new();

        IReadOnlyDictionary<Type, JsonNode> examples = provider.GetExamples();
        JsonNode example = examples[typeof(PostTimelineEntryRequest)];

        example["entryType"]!.GetValue<string>().ShouldBe("Comment");
    }

    [Fact]
    public void GetExamples_PostTimelineEntryRequest_HasBody()
    {
        TimelineSchemaExampleProvider provider = new();

        IReadOnlyDictionary<Type, JsonNode> examples = provider.GetExamples();
        JsonNode example = examples[typeof(PostTimelineEntryRequest)];

        example["body"]!.GetValue<string>().ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public void GetExamples_PostTimelineEntryRequest_HasAttachmentBlobIds()
    {
        TimelineSchemaExampleProvider provider = new();

        IReadOnlyDictionary<Type, JsonNode> examples = provider.GetExamples();
        JsonNode example = examples[typeof(PostTimelineEntryRequest)];

        var blobIds = example["attachmentBlobIds"] as JsonArray;
        blobIds.ShouldNotBeNull();
        blobIds!.Count.ShouldBe(1);
    }
}
