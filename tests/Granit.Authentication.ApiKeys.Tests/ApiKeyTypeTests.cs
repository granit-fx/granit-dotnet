using Shouldly;
using Xunit;

namespace Granit.Authentication.ApiKeys.Tests;

public sealed class ApiKeyTypeTests
{
    [Fact]
    public void ApiKeyType_HasFourValues() =>
        Enum.GetValues<ApiKeyType>().Length.ShouldBe(4);
}
