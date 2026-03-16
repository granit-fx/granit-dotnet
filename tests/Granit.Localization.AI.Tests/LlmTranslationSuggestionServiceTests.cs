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

public sealed class LlmTranslationSuggestionServiceTests
{
    private readonly IAIChatClientFactory _chatClientFactory = Substitute.For<IAIChatClientFactory>();
    private readonly IChatClient _chatClient = Substitute.For<IChatClient>();
    private readonly IOptions<LocalizationAIOptions> _options = Microsoft.Extensions.Options.Options.Create(new LocalizationAIOptions
    {
        WorkspaceName = "test",
        TimeoutSeconds = 30,
    });

    private readonly LlmTranslationSuggestionService _sut;

    public LlmTranslationSuggestionServiceTests()
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
    public async Task SuggestTranslationsAsync_ValidResponse_ReturnsSuggestions()
    {
        // Arrange
        const string jsonResponse = """{"fr": "Soumettre", "de": "Absenden", "es": "Enviar"}""";

        _chatClient
            .GetResponseAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Any<ChatOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns(new ChatResponse(new ChatMessage(ChatRole.Assistant, jsonResponse)));

        string[] targetCultures = ["fr", "de", "es"];

        // Act
        IReadOnlyList<TranslationSuggestion> result = await _sut.SuggestTranslationsAsync(
            "Login:SubmitButton",
            "Submit",
            "en",
            targetCultures,
            TranslationContext.UiLabel,
            TestContext.Current.CancellationToken);

        // Assert
        result.Count.ShouldBe(3);
        result.ShouldContain(s => s.Culture == "fr" && s.Value == "Soumettre");
        result.ShouldContain(s => s.Culture == "de" && s.Value == "Absenden");
        result.ShouldContain(s => s.Culture == "es" && s.Value == "Enviar");
    }

    [Fact]
    public async Task SuggestTranslationsAsync_LLMFailure_ReturnsEmptyList()
    {
        // Arrange
        _chatClient
            .GetResponseAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Any<ChatOptions?>(),
                Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Provider unavailable"));

        string[] targetCultures = ["fr", "de"];

        // Act
        IReadOnlyList<TranslationSuggestion> result = await _sut.SuggestTranslationsAsync(
            "Error:NotFound",
            "Not found",
            "en",
            targetCultures,
            TranslationContext.ErrorMessage,
            TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task SuggestTranslationsAsync_PreservesPlaceholders()
    {
        // Arrange
        const string jsonResponse = """{"fr": "Bonjour {0}, vous avez {1} messages"}""";

        _chatClient
            .GetResponseAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Any<ChatOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns(new ChatResponse(new ChatMessage(ChatRole.Assistant, jsonResponse)));

        string[] targetCultures = ["fr"];

        // Act
        IReadOnlyList<TranslationSuggestion> result = await _sut.SuggestTranslationsAsync(
            "Greeting:Welcome",
            "Hello {0}, you have {1} messages",
            "en",
            targetCultures,
            TranslationContext.Notification,
            TestContext.Current.CancellationToken);

        // Assert
        result.Count.ShouldBe(1);
        result[0].Value.ShouldContain("{0}");
        result[0].Value.ShouldContain("{1}");
    }

    [Fact]
    public async Task SuggestTranslationsAsync_EmptyTargetCultures_ReturnsEmptyList()
    {
        // Arrange
        string[] targetCultures = [];

        // Act
        IReadOnlyList<TranslationSuggestion> result = await _sut.SuggestTranslationsAsync(
            "Some:Key",
            "Some value",
            "en",
            targetCultures,
            TranslationContext.UiLabel,
            TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeEmpty();

        // Verify no LLM call was made
        await _chatClient
            .DidNotReceive()
            .GetResponseAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Any<ChatOptions?>(),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SuggestTranslationsAsync_InvalidJson_ReturnsEmptyList()
    {
        // Arrange
        _chatClient
            .GetResponseAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Any<ChatOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns(new ChatResponse(new ChatMessage(ChatRole.Assistant, "not valid json at all")));

        string[] targetCultures = ["fr"];

        // Act
        IReadOnlyList<TranslationSuggestion> result = await _sut.SuggestTranslationsAsync(
            "Some:Key",
            "Some value",
            "en",
            targetCultures,
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task SuggestTranslationsAsync_MarkdownCodeFences_StripsAndParses()
    {
        // Arrange
        const string jsonWithFences = """
            ```json
            {"nl": "Verzenden"}
            ```
            """;

        _chatClient
            .GetResponseAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Any<ChatOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns(new ChatResponse(new ChatMessage(ChatRole.Assistant, jsonWithFences)));

        string[] targetCultures = ["nl"];

        // Act
        IReadOnlyList<TranslationSuggestion> result = await _sut.SuggestTranslationsAsync(
            "Action:Send",
            "Send",
            "en",
            targetCultures,
            TranslationContext.UiLabel,
            TestContext.Current.CancellationToken);

        // Assert
        result.Count.ShouldBe(1);
        result[0].Culture.ShouldBe("nl");
        result[0].Value.ShouldBe("Verzenden");
    }
}
