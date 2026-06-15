using System.Text.Json;
using Granit.AI.Tools;
using Granit.Localization.AI.Internal;
using Granit.Localization.AI.Permissions;
using NSubstitute;
using Shouldly;

namespace Granit.Localization.AI.Tests;

public sealed class TranslateToolTests
{
    private static AIToolInvocationContext Args(object value) =>
        new() { Arguments = JsonSerializer.SerializeToElement(value) };

    [Fact]
    public void Is_gated_by_the_translate_permission()
    {
        TranslateTool tool = new(Substitute.For<ITranslationSuggestionService>());

        tool.Name.ShouldBe("translate");
        tool.ShouldBeAssignableTo<IGatedAITool>();
        ((IGatedAITool)tool).RequiredPermission.ShouldBe(LocalizationAIPermissions.ChatTools.Translate);
    }

    [Fact]
    public void Schema_requires_text_and_target_language()
    {
        TranslateTool tool = new(Substitute.For<ITranslationSuggestionService>());

        string[] required = [.. tool.ParameterSchema.GetProperty("required").EnumerateArray().Select(e => e.GetString()!)];
        required.ShouldBe(["text", "target_language"]);
    }

    [Fact]
    public async Task Delegates_to_the_translation_service_and_returns_the_translation()
    {
        ITranslationSuggestionService svc = Substitute.For<ITranslationSuggestionService>();
        svc.SuggestTranslationsAsync("chat.translate", "Hello", "en",
                Arg.Is<IReadOnlyList<string>>(t => t.Count == 1 && t[0] == "fr"),
                TranslationContext.Description, Arg.Any<CancellationToken>())
            .Returns([new("fr", "Bonjour")]);
        TranslateTool tool = new(svc);

        AIToolResult result = await tool.InvokeAsync(
            Args(new { text = "Hello", target_language = "fr" }), TestContext.Current.CancellationToken);

        result.IsError.ShouldBeFalse();
        using var payload = JsonDocument.Parse(result.Content);
        payload.RootElement.GetProperty("translation").GetString().ShouldBe("Bonjour");
        payload.RootElement.GetProperty("target_language").GetString().ShouldBe("fr");
    }

    [Fact]
    public async Task Missing_target_language_returns_an_error()
    {
        TranslateTool tool = new(Substitute.For<ITranslationSuggestionService>());

        AIToolResult result = await tool.InvokeAsync(
            Args(new { text = "Hello" }), TestContext.Current.CancellationToken);

        result.IsError.ShouldBeTrue();
    }

    [Fact]
    public async Task Empty_service_result_returns_an_error()
    {
        ITranslationSuggestionService svc = Substitute.For<ITranslationSuggestionService>();
        svc.SuggestTranslationsAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<IReadOnlyList<string>>(), Arg.Any<TranslationContext>(), Arg.Any<CancellationToken>())
            .Returns([]);
        TranslateTool tool = new(svc);

        AIToolResult result = await tool.InvokeAsync(
            Args(new { text = "Hello", target_language = "fr" }), TestContext.Current.CancellationToken);

        result.IsError.ShouldBeTrue();
    }
}
