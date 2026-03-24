using Granit.Exceptions;
using Granit.Vault.HashiCorp.Exceptions;
using Shouldly;
using Xunit;

namespace Granit.Vault.HashiCorp.Tests;

public sealed class HashiCorpVaultConfigurationExceptionTests
{
    [Fact]
    public void Constructor_SetsErrorCode()
    {
        HashiCorpVaultConfigurationException exception = new("Vault:UnknownAuthMethod", "test message");

        exception.ErrorCode.ShouldBe("Vault:UnknownAuthMethod");
    }

    [Fact]
    public void Constructor_SetsMessage()
    {
        HashiCorpVaultConfigurationException exception = new("Vault:TokenRequired", "Token is required");

        exception.Message.ShouldBe("Token is required");
    }

    [Fact]
    public void Exception_ImplementsIHasErrorCode()
    {
        HashiCorpVaultConfigurationException exception = new("code", "msg");

        exception.ShouldBeAssignableTo<IHasErrorCode>();
    }

    [Fact]
    public void Exception_InheritsFromInvalidOperationException()
    {
        HashiCorpVaultConfigurationException exception = new("code", "msg");

        exception.ShouldBeAssignableTo<InvalidOperationException>();
    }

    [Fact]
    public void ErrorCode_IsAccessibleViaInterface()
    {
        HashiCorpVaultConfigurationException exception = new("Vault:Test", "test");

        ((IHasErrorCode)exception).ErrorCode.ShouldBe("Vault:Test");
    }
}
