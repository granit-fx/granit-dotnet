using Granit.Imaging.Exceptions;
using Shouldly;
using Xunit;

namespace Granit.Imaging.Tests.Exceptions;

public sealed class UnsupportedImageFormatExceptionTests
{
    [Fact]
    public void Constructor_SetsDetectedFormat()
    {
        UnsupportedImageFormatException exception = new("HEIC");

        exception.DetectedFormat.ShouldBe("HEIC");
    }

    [Fact]
    public void Constructor_SetsGenericMessage_WithoutLeakingFormat()
    {
        UnsupportedImageFormatException exception = new("HEIC");

        exception.Message.ShouldContain("not supported");
        exception.Message.ShouldNotContain("HEIC");
        exception.DetectedFormat.ShouldBe("HEIC");
    }

    [Fact]
    public void Constructor_InheritsFromException()
    {
        UnsupportedImageFormatException exception = new("RAW");

        exception.ShouldBeAssignableTo<Exception>();
    }

    [Fact]
    public void Type_IsSealed() => typeof(UnsupportedImageFormatException).IsSealed.ShouldBeTrue();

    [Fact]
    public void Constructor_EmptyFormat_StillSetsProperty()
    {
        UnsupportedImageFormatException exception = new("");

        exception.DetectedFormat.ShouldBe("");
    }

    [Fact]
    public void InnerException_IsNull()
    {
        UnsupportedImageFormatException exception = new("SVG");

        exception.InnerException.ShouldBeNull();
    }
}
