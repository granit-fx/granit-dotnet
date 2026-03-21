using Granit.AI;
using Granit.Localization.AI.Internal;
using Granit.Localization.AI.Options;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;

namespace Granit.Localization.AI.Tests;

public sealed class LlmTranslationSuggestionServiceAdditionalTests
{
    private readonly IAIChatClientFactory _chatClientFactory = Substitute.For<IAIChatClientFactory>();
    private readonly IChatClient _chatClient = Substitute.For<IChatClient>();
    private readonly IOptions<LocalizationAIOptions> _options = Microsoft.Extensions.Options.Options.Create(new LocalizationAIOptions
    {
        WorkspaceName = "test",
        TimeoutSeconds = 30,
    });

    private readonly LlmTranslationSuggestionService _sut;

    public LlmTranslationSuggestionServiceAdditionalTests()
    {
        _chatClientFactory
            .CreateAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(_chatClient);

        _sut = new LlmTranslationSuggestionService(
            _chatClientFactory,
            _options,
            NullLogger<LlmTranslationSuggestionService>.Instance);
    }

    [Fact]
    public async Task SuggestTranslationsAsync_NullKey_ThrowsArgumentNullException()
    {
        Func<Task> act = () => _sut.SuggestTranslationsAsync(
            null!,
            "value",
            "en",
            ["fr"],
            cancellationToken: TestContext.Current.CancellationToken);

        await Should.ThrowAsync<ArgumentNullException>(act);
    }

    [Fact]
    public async Task SuggestTranslationsAsync_NullSourceValue_ThrowsArgumentNullException()
    {
        Func<Task> act = () => _sut.SuggestTranslationsAsync(
            "key",
            null!,
            "en",
            ["fr"],
            cancellationToken: TestContext.Current.CancellationToken);

        await Should.ThrowAsync<ArgumentNullException>(act);
    }

    [Fact]
    public async Task SuggestTranslationsAsync_NullSourceCulture_ThrowsArgumentNullException()
    {
        Func<Task> act = () => _sut.SuggestTranslationsAsync(
            "key",
            "value",
            null!,
            ["fr"],
            cancellationToken: TestContext.Current.CancellationToken);

        await Should.ThrowAsync<ArgumentNullException>(act);
    }

    [Fact]
    public async Task SuggestTranslationsAsync_NullTargetCultures_ThrowsArgumentNullException()
    {
        Func<Task> act = () => _sut.SuggestTranslationsAsync(
            "key",
            "value",
            "en",
            null!,
            cancellationToken: TestContext.Current.CancellationToken);

        await Should.ThrowAsync<ArgumentNullException>(act);
    }

    [Fact]
    public async Task SuggestTranslationsAsync_NullJsonResponse_ReturnsEmptyList()
    {
        _chatClient
            .GetResponseAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Any<ChatOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns(new ChatResponse(new ChatMessage(ChatRole.Assistant, "null")));

        string[] targetCultures = ["fr"];

        IReadOnlyList<TranslationSuggestion> result = await _sut.SuggestTranslationsAsync(
            "Some:Key",
            "Some value",
            "en",
            targetCultures,
            cancellationToken: TestContext.Current.CancellationToken);

        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task SuggestTranslationsAsync_PartialResponse_OnlyReturnsMatchingCultures()
    {
        // Response only has "fr" but not "de"
        const string jsonResponse = """{"fr": "Soumettre"}""";

        _chatClient
            .GetResponseAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Any<ChatOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns(new ChatResponse(new ChatMessage(ChatRole.Assistant, jsonResponse)));

        string[] targetCultures = ["fr", "de"];

        IReadOnlyList<TranslationSuggestion> result = await _sut.SuggestTranslationsAsync(
            "Action:Submit",
            "Submit",
            "en",
            targetCultures,
            cancellationToken: TestContext.Current.CancellationToken);

        result.Count.ShouldBe(1);
        result[0].Culture.ShouldBe("fr");
    }

    [Fact]
    public async Task SuggestTranslationsAsync_EmptyValueInResponse_SkipsCulture()
    {
        const string jsonResponse = """{"fr": "Soumettre", "de": ""}""";

        _chatClient
            .GetResponseAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Any<ChatOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns(new ChatResponse(new ChatMessage(ChatRole.Assistant, jsonResponse)));

        string[] targetCultures = ["fr", "de"];

        IReadOnlyList<TranslationSuggestion> result = await _sut.SuggestTranslationsAsync(
            "Action:Submit",
            "Submit",
            "en",
            targetCultures,
            cancellationToken: TestContext.Current.CancellationToken);

        result.Count.ShouldBe(1);
        result[0].Culture.ShouldBe("fr");
    }

    [Theory]
    [InlineData(TranslationContext.UiLabel)]
    [InlineData(TranslationContext.ErrorMessage)]
    [InlineData(TranslationContext.Notification)]
    [InlineData(TranslationContext.Description)]
    [InlineData(TranslationContext.Placeholder)]
    public async Task SuggestTranslationsAsync_AllContextTypes_Work(TranslationContext context)
    {
        const string jsonResponse = """{"fr": "Traduction"}""";

        _chatClient
            .GetResponseAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Any<ChatOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns(new ChatResponse(new ChatMessage(ChatRole.Assistant, jsonResponse)));

        string[] targetCultures = ["fr"];

        IReadOnlyList<TranslationSuggestion> result = await _sut.SuggestTranslationsAsync(
            "Some:Key",
            "Some value",
            "en",
            targetCultures,
            context,
            TestContext.Current.CancellationToken);

        result.Count.ShouldBe(1);
    }

    [Fact]
    public async Task SuggestTranslationsAsync_ResponseWithEmptyText_ReturnsEmptyList()
    {
        _chatClient
            .GetResponseAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Any<ChatOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns(new ChatResponse(new ChatMessage(ChatRole.Assistant, (string?)null)));

        string[] targetCultures = ["fr"];

        IReadOnlyList<TranslationSuggestion> result = await _sut.SuggestTranslationsAsync(
            "Some:Key",
            "Some value",
            "en",
            targetCultures,
            cancellationToken: TestContext.Current.CancellationToken);

        // null text becomes empty string which fails JSON parsing => empty list
        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task SuggestTranslationsAsync_CallerCancelled_ThrowsOperationCancelled()
    {
        using CancellationTokenSource cts = new();
        await cts.CancelAsync();

        _chatClient
            .GetResponseAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Any<ChatOptions?>(),
                Arg.Any<CancellationToken>())
            .ThrowsAsync(new OperationCanceledException());

        string[] targetCultures = ["fr"];

        Func<Task> act = () => _sut.SuggestTranslationsAsync(
            "Some:Key",
            "Some value",
            "en",
            targetCultures,
            cancellationToken: cts.Token);

        await Should.ThrowAsync<OperationCanceledException>(act);
    }
}
