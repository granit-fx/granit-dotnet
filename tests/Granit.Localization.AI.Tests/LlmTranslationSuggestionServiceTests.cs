using Granit.AI;
using Granit.Localization.AI.Internal;
using Granit.Localization.AI.Options;
using Granit.Localization.AI.Schema;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;

namespace Granit.Localization.AI.Tests;

public sealed class LlmTranslationSuggestionServiceTests
{
    private readonly IStructuredCompletion _structured = Substitute.For<IStructuredCompletion>();
    private readonly IOptions<LocalizationAIOptions> _options = Microsoft.Extensions.Options.Options.Create(new LocalizationAIOptions
    {
        WorkspaceName = "test",
        TimeoutSeconds = 30,
    });

    private readonly LlmTranslationSuggestionService _sut;

    public LlmTranslationSuggestionServiceTests() =>
        _sut = new LlmTranslationSuggestionService(
            _structured,
            _options,
            NullLogger<LlmTranslationSuggestionService>.Instance);

    private static StructuredCompletionResult<TranslationsResponse> Ok(params (string Culture, string Value)[] items) =>
        new()
        {
            Status = StructuredCompletionStatus.Succeeded,
            Value = new TranslationsResponse
            {
                Translations = [.. items.Select(i => new TranslationItem { Culture = i.Culture, Value = i.Value })],
            },
        };

    private void Respond(StructuredCompletionResult<TranslationsResponse> result) =>
        _structured
            .CompleteAsync<TranslationsResponse>(Arg.Any<StructuredCompletionRequest>(), Arg.Any<CancellationToken>())
            .Returns(result);

    [Fact]
    public async Task SuggestTranslationsAsync_well_formed_response_returns_one_suggestion_per_culture()
    {
        Respond(Ok(("fr", "Soumettre"), ("de", "Absenden"), ("es", "Enviar")));

        IReadOnlyList<TranslationSuggestion> result = await _sut.SuggestTranslationsAsync(
            "Login:SubmitButton", "Submit", "en", ["fr", "de", "es"],
            TranslationContext.UiLabel, TestContext.Current.CancellationToken);

        result.Count.ShouldBe(3);
        result.ShouldContain(s => s.Culture == "fr" && s.Value == "Soumettre");
        result.ShouldContain(s => s.Culture == "de" && s.Value == "Absenden");
        result.ShouldContain(s => s.Culture == "es" && s.Value == "Enviar");
    }

    [Fact]
    public async Task SuggestTranslationsAsync_routes_source_text_as_content_and_target_cultures_as_instruction()
    {
        StructuredCompletionRequest? captured = null;
        _structured
            .CompleteAsync<TranslationsResponse>(Arg.Do<StructuredCompletionRequest>(r => captured = r), Arg.Any<CancellationToken>())
            .Returns(Ok(("fr", "Soumettre")));

        await _sut.SuggestTranslationsAsync(
            "Login:SubmitButton", "Submit", "en", ["fr"],
            TranslationContext.UiLabel, TestContext.Current.CancellationToken);

        captured.ShouldNotBeNull();
        // Untrusted source value flows through the sanitized Content channel...
        captured.Content.ShouldBe("Submit");
        // ...while the developer-controlled instruction names the requested cultures.
        captured.Instruction!.ShouldContain("fr");
        captured.WorkspaceName.ShouldBe("test");
        // Key and source culture are carried as labelled context, not folded into Content.
        captured.Context.ShouldNotBeNull();
        captured.Context.ShouldContain(kv => kv.Value == "en");
        captured.Context.ShouldContain(kv => kv.Value == "Login:SubmitButton");
    }

    [Fact]
    public async Task SuggestTranslationsAsync_preserves_placeholders_in_returned_value()
    {
        Respond(Ok(("fr", "Bonjour {0}, vous avez {1} messages")));

        IReadOnlyList<TranslationSuggestion> result = await _sut.SuggestTranslationsAsync(
            "Greeting:Welcome", "Hello {0}, you have {1} messages", "en", ["fr"],
            TranslationContext.Notification, TestContext.Current.CancellationToken);

        result.Count.ShouldBe(1);
        result[0].Value.ShouldContain("{0}");
        result[0].Value.ShouldContain("{1}");
    }

    [Fact]
    public async Task SuggestTranslationsAsync_empty_target_cultures_returns_empty_without_calling_the_model()
    {
        IReadOnlyList<TranslationSuggestion> result = await _sut.SuggestTranslationsAsync(
            "Some:Key", "Some value", "en", [],
            TranslationContext.UiLabel, TestContext.Current.CancellationToken);

        result.ShouldBeEmpty();
        await _structured
            .DidNotReceiveWithAnyArgs()
            .CompleteAsync<TranslationsResponse>(default!, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task SuggestTranslationsAsync_drops_cultures_outside_the_requested_set()
    {
        // Model echoes an extra culture nobody asked for — server-side intersection discards it.
        Respond(Ok(("fr", "Soumettre"), ("zz", "injected")));

        IReadOnlyList<TranslationSuggestion> result = await _sut.SuggestTranslationsAsync(
            "Action:Submit", "Submit", "en", ["fr"],
            cancellationToken: TestContext.Current.CancellationToken);

        result.Count.ShouldBe(1);
        result[0].Culture.ShouldBe("fr");
    }

    [Fact]
    public async Task SuggestTranslationsAsync_re_keys_returned_culture_to_the_requested_casing()
    {
        Respond(Ok(("FR", "Soumettre")));

        IReadOnlyList<TranslationSuggestion> result = await _sut.SuggestTranslationsAsync(
            "Action:Submit", "Submit", "en", ["fr"],
            cancellationToken: TestContext.Current.CancellationToken);

        result.Count.ShouldBe(1);
        result[0].Culture.ShouldBe("fr");
    }

    [Fact]
    public async Task SuggestTranslationsAsync_deduplicates_repeated_culture_entries()
    {
        Respond(Ok(("fr", "Soumettre"), ("fr", "Envoyer")));

        IReadOnlyList<TranslationSuggestion> result = await _sut.SuggestTranslationsAsync(
            "Action:Submit", "Submit", "en", ["fr"],
            cancellationToken: TestContext.Current.CancellationToken);

        result.Count.ShouldBe(1);
        result[0].Value.ShouldBe("Soumettre");
    }

    [Theory]
    [InlineData(StructuredCompletionStatus.ModelRefused)]
    [InlineData(StructuredCompletionStatus.SchemaViolation)]
    [InlineData(StructuredCompletionStatus.TransportFailure)]
    public async Task SuggestTranslationsAsync_non_success_status_returns_empty_list(StructuredCompletionStatus status)
    {
        Respond(new StructuredCompletionResult<TranslationsResponse> { Status = status });

        IReadOnlyList<TranslationSuggestion> result = await _sut.SuggestTranslationsAsync(
            "Some:Key", "Some value", "en", ["fr"],
            cancellationToken: TestContext.Current.CancellationToken);

        result.ShouldBeEmpty();
    }
}
