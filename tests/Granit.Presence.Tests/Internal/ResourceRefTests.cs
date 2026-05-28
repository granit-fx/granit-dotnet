using Granit.Presence.Abstractions;
using Shouldly;
using Xunit;

namespace Granit.Presence.Tests.Internal;

public sealed class ResourceRefTests
{
    [Theory]
    [InlineData("a")]
    [InlineData("document")]
    [InlineData("cms.page")]
    [InlineData("kanban-card")]
    [InlineData("workflow_state")]
    [InlineData("a1b2c3")]
    public void Validate_accepts_well_formed_kind(string kind)
    {
        var r = new ResourceRef(kind, "id");
        Should.NotThrow(() => r.Validate());
    }

    [Theory]
    [InlineData("Document")] // uppercase forbidden
    [InlineData("1document")] // must start with letter
    [InlineData("-document")] // must start with letter
    [InlineData(".document")] // must start with letter
    [InlineData("doc ument")] // space forbidden
    [InlineData("doc/ument")] // slash forbidden
    [InlineData("")] // empty
    public void Validate_rejects_invalid_kind(string kind)
    {
        var r = new ResourceRef(kind, "id");
        Should.Throw<ArgumentException>(() => r.Validate());
    }

    [Fact]
    public void Validate_rejects_kind_longer_than_64_chars()
    {
        var r = new ResourceRef(new string('a', 65), "id");
        Should.Throw<ArgumentException>(() => r.Validate());
    }

    [Fact]
    public void Validate_accepts_kind_exactly_64_chars()
    {
        var r = new ResourceRef(new string('a', 64), "id");
        Should.NotThrow(() => r.Validate());
    }

    [Fact]
    public void Validate_rejects_id_longer_than_256_chars()
    {
        var r = new ResourceRef("document", new string('x', 257));
        Should.Throw<ArgumentException>(() => r.Validate());
    }

    [Fact]
    public void Validate_accepts_id_exactly_256_chars()
    {
        var r = new ResourceRef("document", new string('x', 256));
        Should.NotThrow(() => r.Validate());
    }

    [Fact]
    public void Validate_rejects_empty_id()
    {
        var r = new ResourceRef("document", "");
        Should.Throw<ArgumentException>(() => r.Validate());
    }
}
