using Shouldly;
using Xunit;

namespace Granit.BlobStorage.AI.Tests;

public sealed class BlobClassificationTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        List<string> tags = ["financial", "document"];
        BlobClassification classification = new("invoice", 0.95, tags, false);

        classification.Category.ShouldBe("invoice");
        classification.Confidence.ShouldBe(0.95);
        classification.DetectedTags.ShouldBe(tags);
        classification.ContainsPiiInFileName.ShouldBeFalse();
    }

    [Fact]
    public void Constructor_WithPiiDetected_SetsFlag()
    {
        BlobClassification classification = new("identity_document", 0.88, ["personal"], true);

        classification.ContainsPiiInFileName.ShouldBeTrue();
    }

    [Fact]
    public void Constructor_WithEmptyTags_SetsEmptyList()
    {
        BlobClassification classification = new("other", 0.5, [], false);

        classification.DetectedTags.ShouldBeEmpty();
    }

    [Fact]
    public void RecordEquality_WorksCorrectly()
    {
        List<string> tags = ["tag1"];
        BlobClassification a = new("invoice", 0.9, tags, false);
        BlobClassification b = new("invoice", 0.9, tags, false);

        a.ShouldBe(b);
    }
}
