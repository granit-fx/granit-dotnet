using Granit.Identity.Local.AspNetIdentity.Internal;
using Granit.Identity.Local.Exceptions;
using Microsoft.AspNetCore.Identity;
using Shouldly;
using Xunit;

namespace Granit.Identity.Local.AspNetIdentity.Tests.Internal;

public sealed class IdentityResultClassifierTests
{
    [Theory]
    [InlineData("DuplicateUserName", IdentityOperationErrorKind.DuplicateUserName)]
    [InlineData("DuplicateEmail", IdentityOperationErrorKind.DuplicateEmail)]
    [InlineData("InvalidEmail", IdentityOperationErrorKind.InvalidEmail)]
    [InlineData("InvalidUserName", IdentityOperationErrorKind.InvalidUserName)]
    [InlineData("PasswordTooShort", IdentityOperationErrorKind.PasswordPolicy)]
    [InlineData("PasswordRequiresDigit", IdentityOperationErrorKind.PasswordPolicy)]
    [InlineData("ConcurrencyFailure", IdentityOperationErrorKind.Unknown)]
    public void Classifies_error_codes_by_code_not_description(string code, IdentityOperationErrorKind expected)
    {
        var result = IdentityResult.Failed(new IdentityError { Code = code, Description = "irrelevant, localized text" });

        IdentityOperationException ex = result.ToOperationException("Op");

        ex.Errors.ShouldHaveSingleItem().Kind.ShouldBe(expected);
    }

    [Fact]
    public void Null_error_code_is_classified_Unknown_without_throwing()
    {
        // A hand-built IdentityError may leave Code unset (null); classification must not NRE.
        var result = IdentityResult.Failed(new IdentityError { Description = "no code" });

        IdentityOperationException ex = result.ToOperationException("Op");

        ex.Errors.ShouldHaveSingleItem().Kind.ShouldBe(IdentityOperationErrorKind.Unknown);
    }

    [Fact]
    public void IsConflict_true_for_duplicate_username_or_email()
    {
        var result = IdentityResult.Failed(
            new IdentityError { Code = "DuplicateEmail", Description = "x" });

        result.ToOperationException("User creation").IsConflict.ShouldBeTrue();
    }

    [Fact]
    public void IsConflict_false_and_HasPasswordPolicyError_true_for_password_policy()
    {
        var result = IdentityResult.Failed(
            new IdentityError { Code = "PasswordTooShort", Description = "x" });

        IdentityOperationException ex = result.ToOperationException("User creation");

        ex.IsConflict.ShouldBeFalse();
        ex.HasPasswordPolicyError.ShouldBeTrue();
    }

    [Fact]
    public void Preserves_operation_label()
    {
        var result = IdentityResult.Failed(new IdentityError { Code = "DefaultError", Description = "x" });

        result.ToOperationException("Role assignment").Operation.ShouldBe("Role assignment");
    }
}
