using Granit.AI.Prompts.Domain;
using Granit.AI.Prompts.Privacy.DataExport;
using Granit.Privacy.BlobStorage;
using Granit.Privacy.DataExport;
using Granit.Privacy.DataExport.Fragments;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.AI.Prompts.Privacy.Tests;

public sealed class PromptTemplatePrivacyDataProviderTests
{
    private static readonly Guid Subject = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private readonly IPromptTemplateDataManager _dataManager = Substitute.For<IPromptTemplateDataManager>();
    private readonly IStagedFragmentBuilder _fragmentBuilder = Substitute.For<IStagedFragmentBuilder>();

    private PromptTemplatePrivacyDataProvider CreateProvider() => new(_dataManager, _fragmentBuilder);

    private static PrivacyExportContext Context() =>
        new(Guid.NewGuid(), Subject, Subject, TenantId: null, Regulation: "GDPR");

    private static PromptTemplate Prompt(string name) =>
        PromptTemplate.Create(Guid.NewGuid(), Subject, name, "desc", "content");

    [Fact]
    public void Provider_metadata_identifies_the_ai_prompts_scope()
    {
        PromptTemplatePrivacyDataProvider.ProviderName.ShouldBe("ai-prompts");
        PromptTemplatePrivacyDataProvider.DisplayKey.ShouldBe("Privacy.Scopes.AIPrompts");
    }

    [Fact]
    public async Task HasData_is_true_when_the_subject_owns_prompts()
    {
        _dataManager.GetAllForOwnerAsync(Subject, Arg.Any<CancellationToken>()).Returns([Prompt("p")]);

        (await CreateProvider().HasDataAsync(Context(), TestContext.Current.CancellationToken)).ShouldBeTrue();
    }

    [Fact]
    public async Task HasData_is_false_when_the_subject_owns_no_prompts()
    {
        _dataManager.GetAllForOwnerAsync(Subject, Arg.Any<CancellationToken>()).Returns([]);

        (await CreateProvider().HasDataAsync(Context(), TestContext.Current.CancellationToken)).ShouldBeFalse();
    }

    [Fact]
    public async Task Export_yields_nothing_when_the_subject_owns_no_prompts()
    {
        _dataManager.GetAllForOwnerAsync(Subject, Arg.Any<CancellationToken>()).Returns([]);

        List<ExportFragment> fragments = [];
        await foreach (ExportFragment fragment in CreateProvider().ExportAsync(Context(), TestContext.Current.CancellationToken))
        {
            fragments.Add(fragment);
        }

        fragments.ShouldBeEmpty();
        await _fragmentBuilder.DidNotReceive().BuildJsonAsync(
            Arg.Any<PrivacyExportContext>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<object>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Export_builds_one_json_fragment_with_the_owned_prompts()
    {
        _dataManager.GetAllForOwnerAsync(Subject, Arg.Any<CancellationToken>()).Returns([Prompt("Brief")]);

        await foreach (ExportFragment _ in CreateProvider().ExportAsync(Context(), TestContext.Current.CancellationToken))
        {
            // drain
        }

        await _fragmentBuilder.Received(1).BuildJsonAsync(
            Arg.Any<PrivacyExportContext>(),
            "ai-prompts",
            "ai-prompts-templates.json",
            Arg.Is<PromptsExportDto>(d => d.PromptCount == 1 && d.Prompts[0].Name == "Brief"),
            Arg.Any<CancellationToken>());
    }
}
