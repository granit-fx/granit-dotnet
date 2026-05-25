using Granit.AI;
using Granit.TextExtraction.Ocr.AI.Options;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;
using Xunit;
using ExtractionOptions = Granit.TextExtraction.Options.GranitTextExtractionOptions;
using MEOptions = Microsoft.Extensions.Options.Options;

namespace Granit.TextExtraction.Ocr.AI.Tests;

public sealed class AIVisionOcrExtractorTests
{
    private const string Png = "image/png";

    private static (AIVisionOcrExtractor extractor, IChatClient chatClient, IAIChatClientFactory factory)
        CreateExtractor(
            ChatResponse? response = null,
            ExtractionOptions? extractionOptions = null,
            AIVisionOcrOptions? ocrOptions = null,
            IVisionOcrPromptBuilder? promptBuilder = null)
    {
        IChatClient chatClient = Substitute.For<IChatClient>();
        chatClient.GetResponseAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Any<ChatOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(response ?? new ChatResponse { Messages = [new ChatMessage(ChatRole.Assistant, "Extracted body.")] }));

        IAIChatClientFactory factory = Substitute.For<IAIChatClientFactory>();
        factory.CreateAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(chatClient));

        IVisionOcrPromptBuilder prompt = promptBuilder ?? Substitute.For<IVisionOcrPromptBuilder>();
        if (promptBuilder is null)
        {
            prompt.BuildPrompt(Arg.Any<string>(), Arg.Any<int>())
                .Returns("default-prompt");
        }

        AIVisionOcrExtractor extractor = new(
            factory,
            prompt,
            MEOptions.Create(extractionOptions ?? new ExtractionOptions()),
            MEOptions.Create(ocrOptions ?? new AIVisionOcrOptions()),
            NullLogger<AIVisionOcrExtractor>.Instance);

        return (extractor, chatClient, factory);
    }

    private static MemoryStream Bytes(int length) => new(new byte[length]);

    [Theory]
    [InlineData("image/png", true)]
    [InlineData("image/jpeg", true)]
    [InlineData("image/webp", true)]
    [InlineData("image/tiff", true)]
    [InlineData("application/pdf", false)]
    [InlineData("text/plain", false)]
    [InlineData("", false)]
    public void CanHandle_recognises_configured_image_types(string contentType, bool expected)
    {
        (AIVisionOcrExtractor extractor, _, _) = CreateExtractor();
        extractor.CanHandle(contentType).ShouldBe(expected);
    }

    [Fact]
    public async Task Sends_prompt_and_image_data_then_returns_response_text()
    {
        (AIVisionOcrExtractor extractor, IChatClient chatClient, IAIChatClientFactory factory) =
            CreateExtractor(new ChatResponse
            {
                Messages = [new ChatMessage(ChatRole.Assistant, "Extracted document text.")],
            });

        byte[] imageBytes = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]; // PNG magic
        using MemoryStream input = new(imageBytes);

        TextExtractionResult result = await extractor.ExtractAsync(
            input, Png, maxCharLength: 1024, cancellationToken: TestContext.Current.CancellationToken);

        result.Content.ShouldBe("Extracted document text.");
        result.ExtractorName.ShouldBe(AIVisionOcrExtractor.ExtractorName);
        result.IsTruncated.ShouldBeFalse();

        // Inspect the messages sent to the chat client to verify the multimodal request.
        await chatClient.Received(1).GetResponseAsync(
            Arg.Is<IEnumerable<ChatMessage>>(msgs => HasPromptAndImage(msgs, Png, imageBytes)),
            Arg.Any<ChatOptions?>(),
            Arg.Any<CancellationToken>());

        await factory.Received(1).CreateAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Truncates_response_at_max_char_length()
    {
        ChatResponse big = new()
        {
            Messages = [new ChatMessage(ChatRole.Assistant, new string('x', 5_000))],
        };
        (AIVisionOcrExtractor extractor, _, _) = CreateExtractor(big);
        using MemoryStream input = Bytes(8);

        TextExtractionResult result = await extractor.ExtractAsync(
            input, Png, maxCharLength: 100, cancellationToken: TestContext.Current.CancellationToken);

        result.IsTruncated.ShouldBeTrue();
        result.Content.Length.ShouldBe(100);
    }

    [Fact]
    public async Task Body_size_cap_throws_input_too_large()
    {
        ExtractionOptions extraction = new() { MaxBodySizeBytes = 16 };
        (AIVisionOcrExtractor extractor, _, _) = CreateExtractor(extractionOptions: extraction);
        using MemoryStream input = Bytes(4096);

        TextExtraction.Exceptions.TextExtractionException tex =
            await Should.ThrowAsync<TextExtraction.Exceptions.TextExtractionException>(
                async () => await extractor.ExtractAsync(
                    input, Png, maxCharLength: 1024,
                    cancellationToken: TestContext.Current.CancellationToken));

        tex.Reason.ShouldBe("input_too_large");
    }

    [Fact]
    public async Task ChatClient_resolution_failure_returns_skipped_result()
    {
        IAIChatClientFactory factory = Substitute.For<IAIChatClientFactory>();
        factory.CreateAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Throws(new InvalidOperationException("workspace not found"));

        IVisionOcrPromptBuilder prompt = Substitute.For<IVisionOcrPromptBuilder>();
        AIVisionOcrExtractor extractor = new(
            factory, prompt,
            MEOptions.Create(new ExtractionOptions()),
            MEOptions.Create(new AIVisionOcrOptions()),
            NullLogger<AIVisionOcrExtractor>.Instance);
        using MemoryStream input = Bytes(8);

        TextExtractionResult result = await extractor.ExtractAsync(
            input, Png, maxCharLength: 1024, cancellationToken: TestContext.Current.CancellationToken);

        result.Content.ShouldBeEmpty();
        result.IsTruncated.ShouldBeTrue();
    }

    [Fact]
    public async Task ChatClient_call_failure_returns_skipped_result()
    {
        IChatClient chatClient = Substitute.For<IChatClient>();
        chatClient.GetResponseAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Any<ChatOptions?>(),
                Arg.Any<CancellationToken>())
            .Throws(new HttpRequestException("502 Bad Gateway"));

        IAIChatClientFactory factory = Substitute.For<IAIChatClientFactory>();
        factory.CreateAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(chatClient));

        IVisionOcrPromptBuilder prompt = Substitute.For<IVisionOcrPromptBuilder>();
        prompt.BuildPrompt(Arg.Any<string>(), Arg.Any<int>()).Returns("p");

        AIVisionOcrExtractor extractor = new(
            factory, prompt,
            MEOptions.Create(new ExtractionOptions()),
            MEOptions.Create(new AIVisionOcrOptions()),
            NullLogger<AIVisionOcrExtractor>.Instance);
        using MemoryStream input = Bytes(8);

        TextExtractionResult result = await extractor.ExtractAsync(
            input, Png, maxCharLength: 1024, cancellationToken: TestContext.Current.CancellationToken);

        result.Content.ShouldBeEmpty();
        result.IsTruncated.ShouldBeTrue();
    }

    [Fact]
    public async Task Workspace_name_from_options_is_used_to_resolve_chat_client()
    {
        AIVisionOcrOptions opts = new() { WorkspaceName = "vision-ocr-fr" };
        (AIVisionOcrExtractor extractor, _, IAIChatClientFactory factory) = CreateExtractor(ocrOptions: opts);
        using MemoryStream input = Bytes(8);

        await extractor.ExtractAsync(
            input, Png, maxCharLength: 1024, cancellationToken: TestContext.Current.CancellationToken);

        await factory.Received(1).CreateAsync("vision-ocr-fr", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Custom_prompt_builder_is_consulted()
    {
        IVisionOcrPromptBuilder customPrompt = Substitute.For<IVisionOcrPromptBuilder>();
        customPrompt.BuildPrompt(Arg.Any<string>(), Arg.Any<int>()).Returns("custom-prompt-for-test");

        (AIVisionOcrExtractor extractor, IChatClient chatClient, _) =
            CreateExtractor(promptBuilder: customPrompt);
        using MemoryStream input = Bytes(8);

        await extractor.ExtractAsync(
            input, Png, maxCharLength: 512, cancellationToken: TestContext.Current.CancellationToken);

        customPrompt.Received(1).BuildPrompt(Png, 512);
        await chatClient.Received(1).GetResponseAsync(
            Arg.Is<IEnumerable<ChatMessage>>(msgs => MessagesContainPrompt(msgs, "custom-prompt-for-test")),
            Arg.Any<ChatOptions?>(),
            Arg.Any<CancellationToken>());
    }

    private static bool HasPromptAndImage(IEnumerable<ChatMessage> messages, string mime, byte[] imageBytes)
    {
        ChatMessage[] msgs = [.. messages];
        if (msgs.Length != 1) { return false; }
        ChatMessage msg = msgs[0];

        bool hasText = msg.Contents.OfType<TextContent>().Any(t => !string.IsNullOrEmpty(t.Text));
        DataContent? dataPart = msg.Contents.OfType<DataContent>().FirstOrDefault();
        if (dataPart is null) { return false; }

        bool mimeMatches = string.Equals(dataPart.MediaType, mime, StringComparison.OrdinalIgnoreCase);
        bool bytesMatch = dataPart.Data.Span.SequenceEqual(imageBytes);

        return hasText && mimeMatches && bytesMatch;
    }

    private static bool MessagesContainPrompt(IEnumerable<ChatMessage> messages, string expectedPrompt) =>
        messages.SelectMany(m => m.Contents).OfType<TextContent>()
            .Any(t => string.Equals(t.Text, expectedPrompt, StringComparison.Ordinal));
}
