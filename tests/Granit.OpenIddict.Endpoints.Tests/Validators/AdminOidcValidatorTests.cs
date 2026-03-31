using FluentValidation.TestHelper;
using Granit.OpenIddict.Endpoints.Dtos;
using Granit.OpenIddict.Endpoints.Validators;
using Xunit;

namespace Granit.OpenIddict.Endpoints.Tests.Validators;

public sealed class AdminOidcCreateApplicationRequestValidatorTests
{
    private readonly AdminOidcCreateApplicationRequestValidator _validator = new();

    [Fact]
    public void Valid_request_passes()
    {
        AdminOidcCreateApplicationRequest request = new("my-client", "My Client", "secret", "web");
        TestValidationResult<AdminOidcCreateApplicationRequest> result = _validator.TestValidate(request);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Valid_request_with_nulls_passes()
    {
        AdminOidcCreateApplicationRequest request = new("my-client", null, null, null);
        TestValidationResult<AdminOidcCreateApplicationRequest> result = _validator.TestValidate(request);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void ClientId_empty_fails(string? clientId)
    {
        AdminOidcCreateApplicationRequest request = new(clientId!, null, null, null);
        TestValidationResult<AdminOidcCreateApplicationRequest> result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.ClientId);
    }

    [Fact]
    public void ClientId_exceeding_max_length_fails()
    {
        string clientId = new('a', 257);
        AdminOidcCreateApplicationRequest request = new(clientId, null, null, null);
        TestValidationResult<AdminOidcCreateApplicationRequest> result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.ClientId);
    }

    [Fact]
    public void ClientId_at_max_length_passes()
    {
        string clientId = new('a', 256);
        AdminOidcCreateApplicationRequest request = new(clientId, null, null, null);
        TestValidationResult<AdminOidcCreateApplicationRequest> result = _validator.TestValidate(request);
        result.ShouldNotHaveValidationErrorFor(x => x.ClientId);
    }

    [Fact]
    public void DisplayName_exceeding_max_length_fails()
    {
        string displayName = new('a', 257);
        AdminOidcCreateApplicationRequest request = new("valid-client", displayName, null, null);
        TestValidationResult<AdminOidcCreateApplicationRequest> result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.DisplayName);
    }

    [Fact]
    public void DisplayName_at_max_length_passes()
    {
        string displayName = new('a', 256);
        AdminOidcCreateApplicationRequest request = new("valid-client", displayName, null, null);
        TestValidationResult<AdminOidcCreateApplicationRequest> result = _validator.TestValidate(request);
        result.ShouldNotHaveValidationErrorFor(x => x.DisplayName);
    }

    [Fact]
    public void DisplayName_null_passes()
    {
        AdminOidcCreateApplicationRequest request = new("valid-client", null, null, null);
        TestValidationResult<AdminOidcCreateApplicationRequest> result = _validator.TestValidate(request);
        result.ShouldNotHaveValidationErrorFor(x => x.DisplayName);
    }
}

public sealed class AdminOidcCreateScopeRequestValidatorTests
{
    private readonly AdminOidcCreateScopeRequestValidator _validator = new();

    [Fact]
    public void Valid_request_passes()
    {
        AdminOidcCreateScopeRequest request = new("openid", "OpenID", "Standard OpenID scope");
        TestValidationResult<AdminOidcCreateScopeRequest> result = _validator.TestValidate(request);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Valid_request_with_nulls_passes()
    {
        AdminOidcCreateScopeRequest request = new("profile", null, null);
        TestValidationResult<AdminOidcCreateScopeRequest> result = _validator.TestValidate(request);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Name_empty_fails(string? name)
    {
        AdminOidcCreateScopeRequest request = new(name!, null, null);
        TestValidationResult<AdminOidcCreateScopeRequest> result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void Name_exceeding_max_length_fails()
    {
        string name = new('a', 257);
        AdminOidcCreateScopeRequest request = new(name, null, null);
        TestValidationResult<AdminOidcCreateScopeRequest> result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void Name_at_max_length_passes()
    {
        string name = new('a', 256);
        AdminOidcCreateScopeRequest request = new(name, null, null);
        TestValidationResult<AdminOidcCreateScopeRequest> result = _validator.TestValidate(request);
        result.ShouldNotHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void DisplayName_exceeding_max_length_fails()
    {
        string displayName = new('a', 257);
        AdminOidcCreateScopeRequest request = new("valid-scope", displayName, null);
        TestValidationResult<AdminOidcCreateScopeRequest> result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.DisplayName);
    }

    [Fact]
    public void DisplayName_at_max_length_passes()
    {
        string displayName = new('a', 256);
        AdminOidcCreateScopeRequest request = new("valid-scope", displayName, null);
        TestValidationResult<AdminOidcCreateScopeRequest> result = _validator.TestValidate(request);
        result.ShouldNotHaveValidationErrorFor(x => x.DisplayName);
    }

    [Fact]
    public void DisplayName_null_passes()
    {
        AdminOidcCreateScopeRequest request = new("valid-scope", null, null);
        TestValidationResult<AdminOidcCreateScopeRequest> result = _validator.TestValidate(request);
        result.ShouldNotHaveValidationErrorFor(x => x.DisplayName);
    }

    [Fact]
    public void Description_exceeding_max_length_fails()
    {
        string description = new('a', 1025);
        AdminOidcCreateScopeRequest request = new("valid-scope", null, description);
        TestValidationResult<AdminOidcCreateScopeRequest> result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Description);
    }

    [Fact]
    public void Description_at_max_length_passes()
    {
        string description = new('a', 1024);
        AdminOidcCreateScopeRequest request = new("valid-scope", null, description);
        TestValidationResult<AdminOidcCreateScopeRequest> result = _validator.TestValidate(request);
        result.ShouldNotHaveValidationErrorFor(x => x.Description);
    }

    [Fact]
    public void Description_null_passes()
    {
        AdminOidcCreateScopeRequest request = new("valid-scope", null, null);
        TestValidationResult<AdminOidcCreateScopeRequest> result = _validator.TestValidate(request);
        result.ShouldNotHaveValidationErrorFor(x => x.Description);
    }
}
