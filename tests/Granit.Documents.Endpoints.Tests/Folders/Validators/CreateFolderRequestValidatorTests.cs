using FluentValidation.Results;
using Granit.Documents.Domain;
using Granit.Documents.Endpoints.Folders.Dtos;
using Granit.Documents.Endpoints.Folders.Validators;
using Shouldly;
using Xunit;

namespace Granit.Documents.Endpoints.Tests.Folders.Validators;

public sealed class CreateFolderRequestValidatorTests
{
    private readonly CreateFolderRequestValidator _validator = new();

    [Fact]
    public async Task ValidName_PassesValidation()
    {
        var request = new CreateFolderRequest(ParentFolderId: null, Name: "Contracts");

        ValidationResult result = await _validator.ValidateAsync(request, TestContext.Current.CancellationToken);

        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public async Task EmptyName_Fails(string name)
    {
        var request = new CreateFolderRequest(null, name);

        ValidationResult result = await _validator.ValidateAsync(request, TestContext.Current.CancellationToken);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(CreateFolderRequest.Name));
    }

    [Fact]
    public async Task NameTooLong_Fails()
    {
        var request = new CreateFolderRequest(null, new string('a', Folder.MaxNameLength + 1));

        ValidationResult result = await _validator.ValidateAsync(request, TestContext.Current.CancellationToken);

        result.IsValid.ShouldBeFalse();
    }

    [Theory]
    [InlineData("a/b")]
    [InlineData("/leading")]
    [InlineData("trailing/")]
    public async Task NameWithSlash_Fails(string name)
    {
        var request = new CreateFolderRequest(null, name);

        ValidationResult result = await _validator.ValidateAsync(request, TestContext.Current.CancellationToken);

        result.IsValid.ShouldBeFalse();
    }
}
