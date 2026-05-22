using FluentValidation.Results;
using Granit.Settings.Endpoints.Dtos;
using Granit.Settings.Endpoints.Validators;
using Shouldly;
using Xunit;

namespace Granit.Settings.Endpoints.Tests.Validators;

public sealed class BulkUpdateSettingsRequestValidatorTests
{
    private readonly BulkUpdateSettingsRequestValidator _validator = new();

    [Fact]
    public async Task Empty_Settings_List_Is_Invalid()
    {
        BulkUpdateSettingsRequest request = new([]);

        ValidationResult result = await _validator.ValidateAsync(
            request, TestContext.Current.CancellationToken);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "Settings");
    }

    [Fact]
    public async Task Single_Valid_Entry_Is_Valid()
    {
        BulkUpdateSettingsRequest request = new([
            new("App.Setting", "value"),
        ]);

        ValidationResult result = await _validator.ValidateAsync(
            request, TestContext.Current.CancellationToken);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public async Task Null_Entry_Value_Is_Valid()
    {
        BulkUpdateSettingsRequest request = new([
            new("App.Setting", null),
        ]);

        ValidationResult result = await _validator.ValidateAsync(
            request, TestContext.Current.CancellationToken);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public async Task Entry_Count_At_Max_Is_Valid()
    {
        BulkSettingEntry[] entries = Enumerable.Range(0, BulkUpdateSettingsRequestValidator.MaxEntries)
            .Select(i => new BulkSettingEntry($"key-{i}", "v"))
            .ToArray();
        BulkUpdateSettingsRequest request = new(entries);

        ValidationResult result = await _validator.ValidateAsync(
            request, TestContext.Current.CancellationToken);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public async Task Entry_Count_Over_Max_Is_Invalid()
    {
        BulkSettingEntry[] entries = Enumerable.Range(0, BulkUpdateSettingsRequestValidator.MaxEntries + 1)
            .Select(i => new BulkSettingEntry($"key-{i}", "v"))
            .ToArray();
        BulkUpdateSettingsRequest request = new(entries);

        ValidationResult result = await _validator.ValidateAsync(
            request, TestContext.Current.CancellationToken);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.ErrorCode == "Validation:BulkSettingsTooLarge");
    }

    [Fact]
    public async Task Empty_Entry_Key_Is_Invalid()
    {
        BulkUpdateSettingsRequest request = new([
            new("", "value"),
        ]);

        ValidationResult result = await _validator.ValidateAsync(
            request, TestContext.Current.CancellationToken);

        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public async Task Entry_Key_Over_MaxLength_Is_Invalid()
    {
        BulkUpdateSettingsRequest request = new([
            new(new string('x', BulkUpdateSettingsRequestValidator.MaxKeyLength + 1), "value"),
        ]);

        ValidationResult result = await _validator.ValidateAsync(
            request, TestContext.Current.CancellationToken);

        result.IsValid.ShouldBeFalse();
    }
}
