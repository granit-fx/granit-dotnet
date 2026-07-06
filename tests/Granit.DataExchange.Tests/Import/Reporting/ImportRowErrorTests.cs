using Granit.DataExchange.Import.Reporting;
using Shouldly;
using Xunit;

namespace Granit.DataExchange.Tests.Import.Reporting;

public sealed class ImportRowErrorTests
{
    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        IReadOnlyList<string> codes = ["ERR"];
        ImportRowError a = new(1, ImportRowErrorKind.Conversion, codes, "msg");
        ImportRowError b = new(1, ImportRowErrorKind.Conversion, codes, "msg");

        a.ShouldBe(b);
    }

    [Fact]
    public void Equality_DifferentKinds_AreNotEqual()
    {
        IReadOnlyList<string> codes = ["ERR"];
        ImportRowError a = new(1, ImportRowErrorKind.Conversion, codes, "msg");
        ImportRowError b = new(1, ImportRowErrorKind.Persistence, codes, "msg");

        a.ShouldNotBe(b);
    }

    [Theory]
    [InlineData(ImportRowErrorKind.Conversion)]
    [InlineData(ImportRowErrorKind.Validation)]
    [InlineData(ImportRowErrorKind.Persistence)]
    [InlineData(ImportRowErrorKind.Identity)]
    public void AllErrorKinds_AreAccepted(ImportRowErrorKind kind)
    {
        ImportRowError error = new(1, kind, ["E"], "message");

        error.Kind.ShouldBe(kind);
    }
}
