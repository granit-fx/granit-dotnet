using FluentValidation.TestHelper;
using Granit.Identity.Local.Endpoints.Dtos;
using Granit.Identity.Local.Endpoints.Validators;
using Xunit;

namespace Granit.Identity.Local.Endpoints.Tests.Validators;

public sealed class AccountPasswordValidatorTests
{
    [Fact]
    public void ChangePassword_ValidRequest_Passes()
    {
        AccountPasswordChangeRequestValidator validator = new();
        AccountPasswordChangeRequest request = new("OldPass123", "NewPass456!");
        validator.TestValidate(request).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void ChangePassword_EmptyCurrentPassword_Fails()
    {
        AccountPasswordChangeRequestValidator validator = new();
        AccountPasswordChangeRequest request = new("", "NewPass456!");
        validator.TestValidate(request).ShouldHaveValidationErrorFor(x => x.CurrentPassword);
    }

    [Fact]
    public void ChangePassword_ShortNewPassword_Fails()
    {
        AccountPasswordChangeRequestValidator validator = new();
        AccountPasswordChangeRequest request = new("OldPass123", "short");
        validator.TestValidate(request).ShouldHaveValidationErrorFor(x => x.NewPassword);
    }

    [Fact]
    public void ForgotPassword_ValidRequest_Passes()
    {
        AccountForgotPasswordRequestValidator validator = new();
        AccountForgotPasswordRequest request = new("alice@test.com");
        validator.TestValidate(request).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void ForgotPassword_EmptyEmail_Fails()
    {
        AccountForgotPasswordRequestValidator validator = new();
        AccountForgotPasswordRequest request = new("");
        validator.TestValidate(request).ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Fact]
    public void ResetPassword_ValidRequest_Passes()
    {
        AccountPasswordResetRequestValidator validator = new();
        AccountPasswordResetRequest request = new("user-id", "valid-token", "NewPass456!");
        validator.TestValidate(request).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void ResetPassword_EmptyToken_Fails()
    {
        AccountPasswordResetRequestValidator validator = new();
        AccountPasswordResetRequest request = new("user-id", "", "NewPass456!");
        validator.TestValidate(request).ShouldHaveValidationErrorFor(x => x.Token);
    }
}
