using Granit.AI;
using Granit.Localization.AI.Internal;
using Granit.Localization.AI.Options;
using Granit.Localization.AI.Schema;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;

namespace Granit.Localization.AI.Tests;

public sealed class LlmTranslationSuggestionServiceAdditionalTests
{
    private readonly IStructuredCompletion _structured = Substitute.For<IStructuredCompletion>();
    private readonly IOptions<LocalizationAIOptions> _options = Microsoft.Extensions.Options.Options.Create(new LocalizationAIOptions
    {
        WorkspaceName = "test",
        TimeoutSeconds = 30,
    });

    private readonly LlmTranslationSuggestionService _sut;

    public LlmTranslationSuggestionServiceAdditionalTests() =>
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
    public async Task SuggestTranslationsAsync_NullKey_ThrowsArgumentNullException()
    {
        Func<Task> act = () => _sut.SuggestTranslationsAsync(
            null!, "value", "en", ["fr"], cancellationToken: TestContext.Current.CancellationToken);

        await Should.ThrowAsync<ArgumentNullException>(act);
    }

    [Fact]
    public async Task SuggestTranslationsAsync_NullSourceValue_ThrowsArgumentNullException()
    {
        Func<Task> act = () => _sut.SuggestTranslationsAsync(
            "key", null!, "en", ["fr"], cancellationToken: TestContext.Current.CancellationToken);

        await Should.ThrowAsync<ArgumentNullException>(act);
    }

    [Fact]
    public async Task SuggestTranslationsAsync_NullSourceCulture_ThrowsArgumentNullException()
    {
        Func<Task> act = () => _sut.SuggestTranslationsAsync(
            "key", "value", null!, ["fr"], cancellationToken: TestContext.Current.CancellationToken);

        await Should.ThrowAsync<ArgumentNullException>(act);
    }

    [Fact]
    public async Task SuggestTranslationsAsync_NullTargetCultures_ThrowsArgumentNullException()
    {
        Func<Task> act = () => _sut.SuggestTranslationsAsync(
            "key", "value", "en", null!, cancellationToken: TestContext.Current.CancellationToken);

        await Should.ThrowAsync<ArgumentNullException>(act);
    }

    [Fact]
    public async Task SuggestTranslationsAsync_PartialResponse_OnlyReturnsMatchingCultures()
    {
        Respond(Ok(("fr", "Soumettre")));

        IReadOnlyList<TranslationSuggestion> result = await _sut.SuggestTranslationsAsync(
            "Action:Submit", "Submit", "en", ["fr", "de"],
            cancellationToken: TestContext.Current.CancellationToken);

        result.Count.ShouldBe(1);
        result[0].Culture.ShouldBe("fr");
    }

    [Fact]
    public async Task SuggestTranslationsAsync_EmptyValueInResponse_SkipsCulture()
    {
        Respond(Ok(("fr", "Soumettre"), ("de", "")));

        IReadOnlyList<TranslationSuggestion> result = await _sut.SuggestTranslationsAsync(
            "Action:Submit", "Submit", "en", ["fr", "de"],
            cancellationToken: TestContext.Current.CancellationToken);

        result.Count.ShouldBe(1);
        result[0].Culture.ShouldBe("fr");
    }

    [Fact]
    public async Task SuggestTranslationsAsync_SucceededWithEmptyTranslations_ReturnsEmptyList()
    {
        Respond(Ok());

        IReadOnlyList<TranslationSuggestion> result = await _sut.SuggestTranslationsAsync(
            "Some:Key", "Some value", "en", ["fr"],
            cancellationToken: TestContext.Current.CancellationToken);

        result.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(TranslationContext.UiLabel)]
    [InlineData(TranslationContext.ErrorMessage)]
    [InlineData(TranslationContext.Notification)]
    [InlineData(TranslationContext.Description)]
    [InlineData(TranslationContext.Placeholder)]
    public async Task SuggestTranslationsAsync_AllContextTypes_Work(TranslationContext context)
    {
        Respond(Ok(("fr", "Traduction")));

        IReadOnlyList<TranslationSuggestion> result = await _sut.SuggestTranslationsAsync(
            "Some:Key", "Some value", "en", ["fr"],
            context, TestContext.Current.CancellationToken);

        result.Count.ShouldBe(1);
    }

    [Fact]
    public async Task SuggestTranslationsAsync_CallerCancelled_PropagatesOperationCancelled()
    {
        using CancellationTokenSource cts = new();
        await cts.CancelAsync();

        _structured
            .CompleteAsync<TranslationsResponse>(Arg.Any<StructuredCompletionRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new OperationCanceledException());

        Func<Task> act = () => _sut.SuggestTranslationsAsync(
            "Some:Key", "Some value", "en", ["fr"], cancellationToken: cts.Token);

        // Caller cancellation (not the internal timeout) must surface, not be swallowed as "[]".
        await Should.ThrowAsync<OperationCanceledException>(act);
    }
}
