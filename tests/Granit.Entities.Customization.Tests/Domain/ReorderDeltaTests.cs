using Granit.Entities.Customization.Domain.Deltas;
using Shouldly;
using Xunit;

namespace Granit.Entities.Customization.Tests.Domain;

public sealed class ReorderDeltaTests
{
    [Theory]
    [InlineData("a", null, true)]
    [InlineData(null, "a", true)]
    [InlineData(null, null, false)]
    [InlineData("a", "b", false)]
    public void IsAnchorWellFormed_reflects_xor_of_before_after(string? before, string? after, bool expected)
    {
        ReorderDelta delta = new("currency", before, after);
        delta.IsAnchorWellFormed.ShouldBe(expected);
    }
}
