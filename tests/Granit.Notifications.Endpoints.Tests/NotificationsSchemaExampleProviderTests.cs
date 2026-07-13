using System.Text.Json.Nodes;
using Granit.Notifications.Endpoints.Dtos;
using Granit.Notifications.Endpoints.Internal;
using Shouldly;
using Xunit;

namespace Granit.Notifications.Endpoints.Tests;

public sealed class NotificationsSchemaExampleProviderTests
{
    [Fact]
    public void GetExamples_ReturnsNotificationPreferenceUpdateRequestExample()
    {
        var provider = new NotificationsSchemaExampleProvider();

        IReadOnlyDictionary<Type, JsonNode> examples = provider.GetExamples();

        examples.ShouldContainKey(typeof(NotificationPreferenceUpdateRequest));
    }

    [Fact]
    public void GetExamples_ExampleContainsExpectedFields()
    {
        var provider = new NotificationsSchemaExampleProvider();

        IReadOnlyDictionary<Type, JsonNode> examples = provider.GetExamples();
        JsonNode? example = examples[typeof(NotificationPreferenceUpdateRequest)];

        example.ShouldNotBeNull();
        example!["notificationTypeName"]!.GetValue<string>().ShouldBe("NewMessage");
        example["channelName"]!.GetValue<string>().ShouldBe("Email");
        example["isEnabled"]!.GetValue<bool>().ShouldBeTrue();
    }

    [Fact]
    public void GetExamples_ReturnsSingleEntry()
    {
        var provider = new NotificationsSchemaExampleProvider();

        IReadOnlyDictionary<Type, JsonNode> examples = provider.GetExamples();

        examples.Count.ShouldBe(1);
    }
}
