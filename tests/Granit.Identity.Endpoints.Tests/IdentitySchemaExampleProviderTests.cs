using System.Text.Json.Nodes;
using Granit.Identity.Endpoints.Dtos;
using Granit.Identity.Endpoints.Internal;
using Shouldly;
using Xunit;

namespace Granit.Identity.Endpoints.Tests;

public sealed class IdentitySchemaExampleProviderTests
{
    private readonly IdentitySchemaExampleProvider _provider = new();

    [Fact]
    public void GetExamples_ContainsBatchRequestExample()
    {
        IReadOnlyDictionary<Type, JsonNode> examples = _provider.GetExamples();

        examples.ShouldContainKey(typeof(IdentityUserCacheBatchRequest));
        JsonNode batchExample = examples[typeof(IdentityUserCacheBatchRequest)];
        batchExample["userIds"].ShouldNotBeNull();
    }

    [Fact]
    public void GetExamples_ContainsSyncRequestExample()
    {
        IReadOnlyDictionary<Type, JsonNode> examples = _provider.GetExamples();

        examples.ShouldContainKey(typeof(IdentityUserCacheSyncRequest));
        JsonNode syncExample = examples[typeof(IdentityUserCacheSyncRequest)];
        syncExample["userIds"].ShouldNotBeNull();
    }

    [Fact]
    public void GetExamples_ContainsWebhookPayloadExample()
    {
        IReadOnlyDictionary<Type, JsonNode> examples = _provider.GetExamples();

        examples.ShouldContainKey(typeof(IdentityWebhookPayload));
        JsonNode webhookExample = examples[typeof(IdentityWebhookPayload)];
        webhookExample["eventType"]!.GetValue<string>().ShouldBe("user_updated");
        webhookExample["userId"].ShouldNotBeNull();
        webhookExample["timestamp"].ShouldNotBeNull();
    }

    [Fact]
    public void GetExamples_ReturnsThreeExamples()
    {
        IReadOnlyDictionary<Type, JsonNode> examples = _provider.GetExamples();

        examples.Count.ShouldBe(3);
    }
}
