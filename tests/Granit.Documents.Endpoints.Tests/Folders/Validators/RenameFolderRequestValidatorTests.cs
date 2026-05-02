using FluentValidation.Results;
using Granit.Documents.Domain;
using Granit.Documents.Endpoints.Folders.Dtos;
using Granit.Documents.Endpoints.Folders.Validators;
using Shouldly;
using Xunit;

namespace Granit.Documents.Endpoints.Tests.Folders.Validators;

public sealed class RenameFolderRequestValidatorTests
{
    private readonly RenameFolderRequestValidator _validator = new();

    [Fact]
    public async Task ValidName_PassesValidation()
    {
        ValidationResult result = await _validator.ValidateAsync(
            new RenameFolderRequest("Renamed"), TestContext.Current.CancellationToken);

        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public async Task EmptyName_Fails(string name)
    {
        ValidationResult result = await _validator.ValidateAsync(
            new RenameFolderRequest(name), TestContext.Current.CancellationToken);

        result.IsValid.ShouldBeFalse();
    }

    [Theory]
    [InlineData("a/b")]
    [InlineData("contains/slash")]
    public async Task NameWithSlash_Fails(string name)
    {
        ValidationResult result = await _validator.ValidateAsync(
            new RenameFolderRequest(name), TestContext.Current.CancellationToken);

        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public async Task NameTooLong_Fails()
    {
        var request = new RenameFolderRequest(new string('a', Folder.MaxNameLength + 1));

        ValidationResult result = await _validator.ValidateAsync(request, TestContext.Current.CancellationToken);

        result.IsValid.ShouldBeFalse();
    }
}
