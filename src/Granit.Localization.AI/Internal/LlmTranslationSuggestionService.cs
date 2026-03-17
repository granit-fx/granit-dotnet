using System.Text.Json;
using Granit.AI;
using Granit.AI.Internal;
using Granit.Localization.AI.Options;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.Localization.AI.Internal;

/// <summary>
/// LLM-based implementation of <see cref="ITranslationSuggestionService"/> that uses
/// <see cref="IAIChatClientFactory"/> to generate translation suggestions.
/// </summary>
internal sealed partial class LlmTranslationSuggestionService(
    IAIChatClientFactory chatClientFactory,
    IOptions<LocalizationAIOptions> options,
    ILogger<LlmTranslationSuggestionService> logger) : ITranslationSuggestionService
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <inheritdoc />
    public async Task<IReadOnlyList<TranslationSuggestion>> SuggestTranslationsAsync(
        string key,
        string sourceValue,
        string sourceCulture,
        IReadOnlyList<string> targetCultures,
        TranslationContext context = TranslationContext.UiLabel,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(sourceValue);
        ArgumentNullException.ThrowIfNull(sourceCulture);
        ArgumentNullException.ThrowIfNull(targetCultures);

        if (targetCultures.Count == 0)
        {
            return [];
        }

        LocalizationAIOptions localizationOptions = options.Value;

        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(localizationOptions.TimeoutSeconds));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        try
        {
            IChatClient chatClient = await chatClientFactory
                .CreateAsync(localizationOptions.WorkspaceName, linkedCts.Token)
                .ConfigureAwait(false);

            string prompt = BuildPrompt(key, sourceValue, sourceCulture, targetCultures, context);

            var messages = new List<ChatMessage>
            {
                new(ChatRole.User, prompt),
            };

            ChatResponse response = await chatClient
                .GetResponseAsync(messages, cancellationToken: linkedCts.Token)
                .ConfigureAwait(false);

            string responseText = response.Text ?? string.Empty;
            responseText = LlmResponseHelper.StripMarkdownCodeFences(responseText);

            Dictionary<string, string>? translations = JsonSerializer.Deserialize<Dictionary<string, string>>(
                responseText, SerializerOptions);

            if (translations is null)
            {
                LogDeserializationNull();
                return [];
            }

            var suggestions = new List<TranslationSuggestion>(translations.Count);

            foreach (string culture in targetCultures)
            {
                if (translations.TryGetValue(culture, out string? value) && !string.IsNullOrEmpty(value))
                {
                    suggestions.Add(new TranslationSuggestion(culture, value));
                }
            }

            LogTranslationSucceeded(key, suggestions.Count, targetCultures.Count);
            return suggestions;
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
        {
            LogTranslationTimeout(key, localizationOptions.TimeoutSeconds);
            return [];
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (JsonException ex)
        {
            LogDeserializationFailed(key, ex.Message);
            return [];
        }
        catch (Exception ex)
        {
            LogTranslationFailed(key, ex.Message);
            return [];
        }
    }

    private static string BuildPrompt(
        string key,
        string sourceValue,
        string sourceCulture,
        IReadOnlyList<string> targetCultures,
        TranslationContext context)
    {
        string contextLabel = context switch
        {
            TranslationContext.UiLabel => "a short UI label (button, menu, header)",
            TranslationContext.ErrorMessage => "a validation or error message",
            TranslationContext.Notification => "a push/email notification text",
            TranslationContext.Description => "a longer descriptive text (tooltip, help text)",
            TranslationContext.Placeholder => "an input field placeholder text",
            _ => "a UI text",
        };

        string cultures = string.Join(", ", targetCultures);
        string exampleJson = "{ " + string.Join(", ", targetCultures.Select(c => $"\"{c}\": \"...\"")) + " }";

        return $"""
            Translate the following text to the requested languages.
            Context: this is {contextLabel}.

            Source ({sourceCulture}): "{sourceValue}"
            Key: "{key}" (for context only, do not translate the key)

            Target languages: {cultures}

            Return a JSON object where keys are culture codes and values are translations:
            {exampleJson}

            Rules:
            - Keep the same tone and formality level as the source
            - For regional variants (fr-CA, en-GB, pt-BR), only include if different from base
            - Preserve placeholders like {"{0}"}, {"{1}"} exactly as-is
            - Return ONLY the JSON, no markdown
            """;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Translation succeeded for key {Key}: {TranslatedCount}/{RequestedCount} cultures")]
    private partial void LogTranslationSucceeded(string key, int translatedCount, int requestedCount);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Translation timed out for key {Key} after {TimeoutSeconds}s")]
    private partial void LogTranslationTimeout(string key, int timeoutSeconds);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Translation failed for key {Key}: {ErrorMessage}")]
    private partial void LogTranslationFailed(string key, string errorMessage);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Deserialization of translation response returned null")]
    private partial void LogDeserializationNull();

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to deserialize translation response for key {Key}: {ErrorMessage}")]
    private partial void LogDeserializationFailed(string key, string errorMessage);
}
