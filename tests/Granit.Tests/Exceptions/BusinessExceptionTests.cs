// =============================================================================
// Tests - BusinessException
// =============================================================================
// Verifies:
//   - Constructor initializes ErrorCode and Message correctly
//   - Default message falls back to the error code
//   - Inner exception is propagated
//   - Implements IHasErrorCode and IUserFriendlyException
// =============================================================================

using Granit.Exceptions;
using Shouldly;
using Xunit;

namespace Granit.Tests.Exceptions;

public sealed class BusinessExceptionTests
{
    // -------------------------------------------------------------------------
    // Constructor
    // -------------------------------------------------------------------------

    [Fact]
    public void Constructor_SetsErrorCode()
    {
        BusinessException exception = new("Vault:CredentialsFailed");

        exception.ErrorCode.ShouldBe("Vault:CredentialsFailed");
    }

    [Fact]
    public void Constructor_WithMessage_SetsMessage()
    {
        BusinessException exception = new("Vault:CredentialsFailed", "Connexion Vault échouée.");

        exception.Message.ShouldBe("Connexion Vault échouée.");
    }

    [Fact]
    public void Constructor_WithoutMessage_UsesErrorCodeAsMessage()
    {
        BusinessException exception = new("Vault:CredentialsFailed");

        exception.Message.ShouldBe("Vault:CredentialsFailed");
    }

    [Fact]
    public void Constructor_WithInnerException_PropagatesIt()
    {
        InvalidOperationException inner = new("inner");
        BusinessException exception = new("Vault:CredentialsFailed", "message", inner);

        exception.InnerException.ShouldBeSameAs(inner);
    }

    // -------------------------------------------------------------------------
    // Interface implementation
    // -------------------------------------------------------------------------

    [Fact]
    public void Implements_IHasErrorCode()
    {
        BusinessException exception = new("Vault:CredentialsFailed");

        exception.ShouldBeAssignableTo<IHasErrorCode>();
        ((IHasErrorCode)exception).ErrorCode.ShouldBe("Vault:CredentialsFailed");
    }

    [Fact]
    public void Implements_IUserFriendlyException()
    {
        BusinessException exception = new("Vault:CredentialsFailed");

        exception.ShouldBeAssignableTo<IUserFriendlyException>();
    }

    [Fact]
    public void IsException()
    {
        BusinessException exception = new("Vault:CredentialsFailed");

        exception.ShouldBeAssignableTo<Exception>();
    }
}
