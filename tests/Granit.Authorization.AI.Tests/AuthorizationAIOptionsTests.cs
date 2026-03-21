using Granit.Authorization.AI.Options;
using Shouldly;

namespace Granit.Authorization.AI.Tests;

public sealed class AuthorizationAIOptionsTests
{
    [Fact]
    public void SectionName_IsAIAuthorization() => AuthorizationAIOptions.SectionName.ShouldBe("AI:Authorization");

    [Fact]
    public void WorkspaceName_Default_IsDefault()
    {
        AuthorizationAIOptions options = new();

        options.WorkspaceName.ShouldBe("default");
    }

    [Fact]
    public void TimeoutSeconds_Default_IsFive()
    {
        AuthorizationAIOptions options = new();

        options.TimeoutSeconds.ShouldBe(5);
    }

    [Fact]
    public void WorkspaceName_CanBeModified()
    {
        AuthorizationAIOptions options = new();

        options.WorkspaceName = "security";

        options.WorkspaceName.ShouldBe("security");
    }

    [Fact]
    public void TimeoutSeconds_CanBeModified()
    {
        AuthorizationAIOptions options = new();

        options.TimeoutSeconds = 10;

        options.TimeoutSeconds.ShouldBe(10);
    }
}
