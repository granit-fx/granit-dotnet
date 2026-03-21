using Granit.BlobStorage.AI.Options;
using Shouldly;
using Xunit;

namespace Granit.BlobStorage.AI.Tests;

public sealed class BlobStorageAIOptionsTests
{
    [Fact]
    public void SectionName_IsExpected() => BlobStorageAIOptions.SectionName.ShouldBe("AI:BlobStorage");

    [Fact]
    public void DefaultWorkspaceName_IsNull()
    {
        BlobStorageAIOptions options = new();

        options.WorkspaceName.ShouldBeNull();
    }

    [Fact]
    public void DefaultTimeoutSeconds_Is10()
    {
        BlobStorageAIOptions options = new();

        options.TimeoutSeconds.ShouldBe(10);
    }

    [Fact]
    public void DefaultEnablePiiDetection_IsTrue()
    {
        BlobStorageAIOptions options = new();

        options.EnablePiiDetection.ShouldBeTrue();
    }

    [Fact]
    public void Properties_CanBeCustomized()
    {
        BlobStorageAIOptions options = new()
        {
            WorkspaceName = "custom-workspace",
            TimeoutSeconds = 30,
            EnablePiiDetection = false,
        };

        options.WorkspaceName.ShouldBe("custom-workspace");
        options.TimeoutSeconds.ShouldBe(30);
        options.EnablePiiDetection.ShouldBeFalse();
    }
}
