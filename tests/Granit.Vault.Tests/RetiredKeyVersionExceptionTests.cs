using Granit.Vault.Exceptions;
using Shouldly;
using Xunit;

namespace Granit.Vault.Tests;

public sealed class RetiredKeyVersionExceptionTests
{
    [Fact]
    public void Constructor_SetsKeyVersion()
    {
        RetiredKeyVersionException exception = new("v1");

        exception.KeyVersion.ShouldBe("v1");
    }

    [Fact]
    public void Constructor_SetsMessage_ContainingKeyVersion()
    {
        RetiredKeyVersionException exception = new("v2");

        exception.Message.ShouldContain("v2");
        exception.Message.ShouldContain("retired");
    }

    [Fact]
    public void Exception_InheritsFromInvalidOperationException()
    {
        RetiredKeyVersionException exception = new("v1");

        exception.ShouldBeAssignableTo<InvalidOperationException>();
    }

    [Fact]
    public void Constructor_WithDifferentVersions_SetsCorrectKeyVersion()
    {
        RetiredKeyVersionException exceptionV3 = new("v3");
        RetiredKeyVersionException exceptionV10 = new("v10");

        exceptionV3.KeyVersion.ShouldBe("v3");
        exceptionV10.KeyVersion.ShouldBe("v10");
    }

    [Fact]
    public void Message_SuggestsRunningReEncryptionJob()
    {
        RetiredKeyVersionException exception = new("v1");

        exception.Message.ShouldContain("re-encryption");
    }
}
