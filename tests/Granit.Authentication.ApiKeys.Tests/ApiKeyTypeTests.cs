using Shouldly;
using Xunit;

namespace Granit.Authentication.ApiKeys.Tests;

public sealed class ApiKeyTypeTests
{
    [Theory]
    [InlineData(ApiKeyType.Secret, 0)]
    [InlineData(ApiKeyType.Publishable, 1)]
    [InlineData(ApiKeyType.Webhook, 2)]
    [InlineData(ApiKeyType.Ephemeral, 3)]
    public void ApiKeyType_HasExpectedIntValue(ApiKeyType type, int expectedValue) =>
        ((int)type).ShouldBe(expectedValue);

    [Fact]
    public void ApiKeyType_HasFourValues() =>
        Enum.GetValues<ApiKeyType>().Length.ShouldBe(4);

    [Theory]
    [InlineData("Secret", ApiKeyType.Secret)]
    [InlineData("Publishable", ApiKeyType.Publishable)]
    [InlineData("Webhook", ApiKeyType.Webhook)]
    [InlineData("Ephemeral", ApiKeyType.Ephemeral)]
    public void ApiKeyType_ParsesFromString(string name, ApiKeyType expected) =>
        Enum.Parse<ApiKeyType>(name).ShouldBe(expected);
}
