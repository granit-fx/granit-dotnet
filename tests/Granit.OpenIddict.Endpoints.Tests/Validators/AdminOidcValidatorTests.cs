using FluentValidation.TestHelper;
using Granit.OpenIddict.Endpoints.Dtos;
using Granit.OpenIddict.Endpoints.Validators;
using Xunit;

#pragma warning disable CA1812 // Instantiated by xUnit

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

public sealed class AdminOidcUpdateApplicationRequestValidatorTests
{
    private readonly AdminOidcUpdateApplicationRequestValidator _validator = new();

    [Fact]
    public void All_null_fields_passes()
    {
        AdminOidcUpdateApplicationRequest request = new();
        TestValidationResult<AdminOidcUpdateApplicationRequest> result = _validator.TestValidate(request);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Valid_partial_update_passes()
    {
        AdminOidcUpdateApplicationRequest request = new(
            DisplayName: "New Name",
            Permissions: ["ept:token"],
            RedirectUris: ["https://example.com/callback"]);
        TestValidationResult<AdminOidcUpdateApplicationRequest> result = _validator.TestValidate(request);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void DisplayName_exceeding_max_length_fails()
    {
        AdminOidcUpdateApplicationRequest request = new(DisplayName: new string('a', 257));
        TestValidationResult<AdminOidcUpdateApplicationRequest> result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.DisplayName);
    }

    [Fact]
    public void DisplayName_at_max_length_passes()
    {
        AdminOidcUpdateApplicationRequest request = new(DisplayName: new string('a', 256));
        TestValidationResult<AdminOidcUpdateApplicationRequest> result = _validator.TestValidate(request);
        result.ShouldNotHaveValidationErrorFor(x => x.DisplayName);
    }

    [Fact]
    public void ConsentType_exceeding_max_length_fails()
    {
        AdminOidcUpdateApplicationRequest request = new(ConsentType: new string('a', 65));
        TestValidationResult<AdminOidcUpdateApplicationRequest> result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.ConsentType);
    }

    [Fact]
    public void ConsentType_at_max_length_passes()
    {
        AdminOidcUpdateApplicationRequest request = new(ConsentType: new string('a', 64));
        TestValidationResult<AdminOidcUpdateApplicationRequest> result = _validator.TestValidate(request);
        result.ShouldNotHaveValidationErrorFor(x => x.ConsentType);
    }

    [Fact]
    public void SigningKeyJwk_exceeding_max_length_fails()
    {
        AdminOidcUpdateApplicationRequest request = new(SigningKeyJwk: new string('a', 65537));
        TestValidationResult<AdminOidcUpdateApplicationRequest> result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.SigningKeyJwk);
    }

    [Fact]
    public void SigningKeyJwk_at_max_length_passes()
    {
        AdminOidcUpdateApplicationRequest request = new(SigningKeyJwk: new string('a', 65536));
        TestValidationResult<AdminOidcUpdateApplicationRequest> result = _validator.TestValidate(request);
        result.ShouldNotHaveValidationErrorFor(x => x.SigningKeyJwk);
    }

    [Fact]
    public void Permission_empty_string_in_array_fails()
    {
        AdminOidcUpdateApplicationRequest request = new(Permissions: ["ept:token", ""]);
        TestValidationResult<AdminOidcUpdateApplicationRequest> result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor("Permissions[1]");
    }

    [Fact]
    public void RedirectUri_without_scheme_fails()
    {
        AdminOidcUpdateApplicationRequest request = new(RedirectUris: ["not-a-valid-uri"]);
        TestValidationResult<AdminOidcUpdateApplicationRequest> result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor("RedirectUris[0]");
    }

    [Fact]
    public void RedirectUri_absolute_uri_passes()
    {
        AdminOidcUpdateApplicationRequest request = new(RedirectUris: ["https://example.com/callback"]);
        TestValidationResult<AdminOidcUpdateApplicationRequest> result = _validator.TestValidate(request);
        result.ShouldNotHaveValidationErrorFor("RedirectUris[0]");
    }

    [Fact]
    public void PostLogoutRedirectUri_relative_uri_fails()
    {
        AdminOidcUpdateApplicationRequest request = new(PostLogoutRedirectUris: ["not-absolute"]);
        TestValidationResult<AdminOidcUpdateApplicationRequest> result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor("PostLogoutRedirectUris[0]");
    }

    [Fact]
    public void Null_Permissions_skips_element_validation()
    {
        AdminOidcUpdateApplicationRequest request = new(Permissions: null);
        TestValidationResult<AdminOidcUpdateApplicationRequest> result = _validator.TestValidate(request);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Null_RedirectUris_skips_element_validation()
    {
        AdminOidcUpdateApplicationRequest request = new(RedirectUris: null);
        TestValidationResult<AdminOidcUpdateApplicationRequest> result = _validator.TestValidate(request);
        result.ShouldNotHaveAnyValidationErrors();
    }
}

public sealed class AdminOidcUpdateScopeRequestValidatorTests
{
    private readonly AdminOidcUpdateScopeRequestValidator _validator = new();

    [Fact]
    public void All_null_fields_passes()
    {
        AdminOidcUpdateScopeRequest request = new();
        TestValidationResult<AdminOidcUpdateScopeRequest> result = _validator.TestValidate(request);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Valid_partial_update_passes()
    {
        AdminOidcUpdateScopeRequest request = new(
            DisplayName: "OpenID Connect",
            Description: "Standard OpenID scope",
            Resources: ["api://granit"]);
        TestValidationResult<AdminOidcUpdateScopeRequest> result = _validator.TestValidate(request);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void DisplayName_exceeding_max_length_fails()
    {
        AdminOidcUpdateScopeRequest request = new(DisplayName: new string('a', 257));
        TestValidationResult<AdminOidcUpdateScopeRequest> result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.DisplayName);
    }

    [Fact]
    public void DisplayName_at_max_length_passes()
    {
        AdminOidcUpdateScopeRequest request = new(DisplayName: new string('a', 256));
        TestValidationResult<AdminOidcUpdateScopeRequest> result = _validator.TestValidate(request);
        result.ShouldNotHaveValidationErrorFor(x => x.DisplayName);
    }

    [Fact]
    public void Description_exceeding_max_length_fails()
    {
        AdminOidcUpdateScopeRequest request = new(Description: new string('a', 1025));
        TestValidationResult<AdminOidcUpdateScopeRequest> result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Description);
    }

    [Fact]
    public void Description_at_max_length_passes()
    {
        AdminOidcUpdateScopeRequest request = new(Description: new string('a', 1024));
        TestValidationResult<AdminOidcUpdateScopeRequest> result = _validator.TestValidate(request);
        result.ShouldNotHaveValidationErrorFor(x => x.Description);
    }

    [Fact]
    public void Resource_empty_string_in_array_fails()
    {
        AdminOidcUpdateScopeRequest request = new(Resources: ["api://valid", ""]);
        TestValidationResult<AdminOidcUpdateScopeRequest> result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor("Resources[1]");
    }

    [Fact]
    public void Resource_exceeding_max_length_fails()
    {
        AdminOidcUpdateScopeRequest request = new(Resources: [new string('a', 513)]);
        TestValidationResult<AdminOidcUpdateScopeRequest> result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor("Resources[0]");
    }

    [Fact]
    public void Empty_resources_array_passes()
    {
        AdminOidcUpdateScopeRequest request = new(Resources: []);
        TestValidationResult<AdminOidcUpdateScopeRequest> result = _validator.TestValidate(request);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Null_resources_skips_element_validation()
    {
        AdminOidcUpdateScopeRequest request = new(Resources: null);
        TestValidationResult<AdminOidcUpdateScopeRequest> result = _validator.TestValidate(request);
        result.ShouldNotHaveAnyValidationErrors();
    }
}

public sealed class AdminOidcCreateAuthorizationRequestValidatorTests
{
    private readonly AdminOidcCreateAuthorizationRequestValidator _validator = new();

    [Fact]
    public void Valid_request_passes()
    {
        AdminOidcCreateAuthorizationRequest request = new("user-abc", "my-client", ["openid", "profile"]);
        TestValidationResult<AdminOidcCreateAuthorizationRequest> result = _validator.TestValidate(request);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Subject_empty_fails(string? subject)
    {
        AdminOidcCreateAuthorizationRequest request = new(subject!, "my-client", ["openid"]);
        TestValidationResult<AdminOidcCreateAuthorizationRequest> result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Subject);
    }

    [Fact]
    public void Subject_exceeding_max_length_fails()
    {
        AdminOidcCreateAuthorizationRequest request = new(new string('a', 257), "my-client", ["openid"]);
        TestValidationResult<AdminOidcCreateAuthorizationRequest> result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Subject);
    }

    [Fact]
    public void Subject_at_max_length_passes()
    {
        AdminOidcCreateAuthorizationRequest request = new(new string('a', 256), "my-client", ["openid"]);
        TestValidationResult<AdminOidcCreateAuthorizationRequest> result = _validator.TestValidate(request);
        result.ShouldNotHaveValidationErrorFor(x => x.Subject);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void ClientId_empty_fails(string? clientId)
    {
        AdminOidcCreateAuthorizationRequest request = new("user-abc", clientId!, ["openid"]);
        TestValidationResult<AdminOidcCreateAuthorizationRequest> result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.ClientId);
    }

    [Fact]
    public void ClientId_exceeding_max_length_fails()
    {
        AdminOidcCreateAuthorizationRequest request = new("user-abc", new string('a', 257), ["openid"]);
        TestValidationResult<AdminOidcCreateAuthorizationRequest> result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.ClientId);
    }

    [Fact]
    public void Scopes_empty_fails()
    {
        AdminOidcCreateAuthorizationRequest request = new("user-abc", "my-client", []);
        TestValidationResult<AdminOidcCreateAuthorizationRequest> result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Scopes);
    }
}
