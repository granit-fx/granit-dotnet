using Granit.DataExchange.Import.Reporting;
using Shouldly;
using Xunit;

namespace Granit.DataExchange.Tests.Import.Reporting;

public sealed class ImportRowErrorTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        ImportRowError error = new(
            42,
            ImportRowErrorKind.Validation,
            ["Validation:NotEmpty", "Validation:MaxLength"],
            "Name is required. Name must be at most 100 characters.");

        error.RowNumber.ShouldBe(42);
        error.Kind.ShouldBe(ImportRowErrorKind.Validation);
        error.ErrorCodes.Count.ShouldBe(2);
        error.ErrorCodes[0].ShouldBe("Validation:NotEmpty");
        error.ErrorCodes[1].ShouldBe("Validation:MaxLength");
        error.Message.ShouldBe("Name is required. Name must be at most 100 characters.");
    }

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
