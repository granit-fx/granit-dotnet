using Granit.DataExchange.Import.Parsing;
using Shouldly;
using Xunit;

namespace Granit.DataExchange.Tests.Import.Parsing;

public sealed class RawImportRowTests
{
    [Fact]
    public void Constructor_WithNullValue()
    {
        Dictionary<string, string?> values = new()
        {
            ["Name"] = "Bob",
            ["Phone"] = null,
        };

        RawImportRow row = new(5, values);

        row.Values["Phone"].ShouldBeNull();
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        IReadOnlyDictionary<string, string?> values = new Dictionary<string, string?>
        {
            ["A"] = "1",
        };

        RawImportRow a = new(1, values);
        RawImportRow b = new(1, values);

        a.ShouldBe(b);
    }

    [Fact]
    public void Equality_DifferentRowNumbers_AreNotEqual()
    {
        IReadOnlyDictionary<string, string?> values = new Dictionary<string, string?>
        {
            ["A"] = "1",
        };

        RawImportRow a = new(1, values);
        RawImportRow b = new(2, values);

        a.ShouldNotBe(b);
    }
}
