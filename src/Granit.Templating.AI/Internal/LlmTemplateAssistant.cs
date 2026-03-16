using System.Reflection;
using System.Text;
using Granit.AI;
using Granit.Templating.AI.Options;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.Templating.AI.Internal;

/// <summary>
/// LLM-backed implementation of <see cref="IAITemplateAssistant"/> that uses
/// <see cref="IAIChatClientFactory"/> to generate Scriban template drafts.
/// </summary>
/// <remarks>
/// Only data type metadata (property names and types) is sent to the LLM — never business data (GDPR safe).
/// On failure, gracefully degrades to <c>null</c>.
/// </remarks>
internal sealed partial class LlmTemplateAssistant(
    IAIChatClientFactory chatClientFactory,
    IOptions<TemplatingAIOptions> options,
    ILogger<LlmTemplateAssistant> logger) : IAITemplateAssistant
{
    /// <inheritdoc/>
    public async Task<string?> GenerateDraftAsync(
        string description,
        Type dataType,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(description);
        ArgumentNullException.ThrowIfNull(dataType);

        TemplatingAIOptions opts = options.Value;

        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(opts.TimeoutSeconds));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        try
        {
            IChatClient chatClient = await chatClientFactory
                .CreateAsync(opts.WorkspaceName, linkedCts.Token)
                .ConfigureAwait(false);

            string prompt = BuildPrompt(description, dataType);

            ChatResponse response = await chatClient
                .GetResponseAsync(prompt, cancellationToken: linkedCts.Token)
                .ConfigureAwait(false);

            string responseText = response.Text ?? string.Empty;

            LogLlmResponseReceived(responseText.Length);

            string template = StripMarkdownFences(responseText);

            return string.IsNullOrWhiteSpace(template) ? null : template;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            LogTemplateGenerationFailed(ex);
            return null;
        }
    }

    internal static string BuildPrompt(string description, Type dataType)
    {
        var sb = new StringBuilder();

        sb.AppendLine("Generate a Scriban HTML template for the following purpose:");
        sb.Append('"');
        sb.Append(description);
        sb.Append('"');
        sb.AppendLine();
        sb.AppendLine();
        sb.AppendLine("Available data properties:");
        sb.AppendLine("| Property | Type |");
        sb.AppendLine("|----------|------|");

        PropertyInfo[] properties = dataType.GetProperties(BindingFlags.Public | BindingFlags.Instance);

        foreach (PropertyInfo property in properties)
        {
            sb.Append("| ");
            sb.Append(property.Name);
            sb.Append(" | ");
            sb.Append(GetFriendlyTypeName(property.PropertyType));
            sb.AppendLine(" |");
        }

        sb.AppendLine();
        sb.AppendLine("Use Scriban syntax: {{ variable.property_name }}");
        sb.AppendLine("Property names in Scriban use snake_case (e.g., InvoiceDate → invoice_date).");
        sb.AppendLine();
        sb.AppendLine("Return ONLY the HTML template, no markdown fences.");
        sb.AppendLine("Include a clean, professional layout with inline CSS.");

        return sb.ToString();
    }

    internal static string StripMarkdownFences(string text)
    {
        string trimmed = text.Trim();

        if (!trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            return trimmed;
        }

        int firstNewline = trimmed.IndexOf('\n');
        if (firstNewline >= 0)
        {
            trimmed = trimmed[(firstNewline + 1)..];
        }

        if (trimmed.EndsWith("```", StringComparison.Ordinal))
        {
            trimmed = trimmed[..^3].TrimEnd();
        }

        return trimmed;
    }

    private static string GetFriendlyTypeName(Type type)
    {
        Type? nullableUnderlying = Nullable.GetUnderlyingType(type);
        if (nullableUnderlying is not null)
        {
            return GetFriendlyTypeName(nullableUnderlying) + "?";
        }

        if (type.IsGenericType)
        {
            string baseName = type.Name[..type.Name.IndexOf('`')];
            string args = string.Join(", ", type.GetGenericArguments().Select(GetFriendlyTypeName));
            return $"{baseName}<{args}>";
        }

        return type switch
        {
            _ when type == typeof(string) => "String",
            _ when type == typeof(int) => "Int32",
            _ when type == typeof(long) => "Int64",
            _ when type == typeof(decimal) => "Decimal",
            _ when type == typeof(double) => "Double",
            _ when type == typeof(float) => "Single",
            _ when type == typeof(bool) => "Boolean",
            _ when type == typeof(DateTime) => "DateTime",
            _ when type == typeof(DateTimeOffset) => "DateTimeOffset",
            _ when type == typeof(DateOnly) => "DateOnly",
            _ when type == typeof(TimeOnly) => "TimeOnly",
            _ when type == typeof(Guid) => "Guid",
            _ => type.Name,
        };
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "LLM response received for template generation ({ResponseLength} chars)")]
    private partial void LogLlmResponseReceived(int responseLength);

    [LoggerMessage(Level = LogLevel.Warning, Message = "AI template generation failed, returning null (graceful degradation)")]
    private partial void LogTemplateGenerationFailed(Exception exception);
}
