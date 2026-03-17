using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Granit.Notifications.Zulip.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.Notifications.Zulip.Internal;

/// <summary>
/// <see cref="IZulipSender"/> implementation using the Zulip Bot REST API.
/// Sends messages via <c>POST /api/v1/messages</c>.
/// </summary>
internal sealed partial class ZulipBotSender(
    IHttpClientFactory httpClientFactory,
    IOptionsMonitor<ZulipBotOptions> options,
    ILogger<ZulipBotSender> logger) : IZulipSender
{
    internal const string HttpClientName = "ZulipBot";

    /// <inheritdoc />
    public async Task SendAsync(ZulipMessage message, CancellationToken cancellationToken = default)
    {
        HttpClient client = httpClientFactory.CreateClient(HttpClientName);
        ZulipBotOptions botOptions = options.CurrentValue;

        string credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{botOptions.BotEmail}:{botOptions.ApiKey}"));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);

        var formData = new List<KeyValuePair<string, string>>
        {
            new("type", message.Type),
            new("content", message.Content),
        };

        if (message.Type == "stream")
        {
            formData.Add(new("to", message.Stream!));
            formData.Add(new("topic", message.Topic!));
        }
        else if (message.To is { Count: > 0 })
        {
            formData.Add(new("to", JsonSerializer.Serialize(message.To)));
        }

        using var content = new FormUrlEncodedContent(formData);
        using HttpResponseMessage response = await client.PostAsync("api/v1/messages", content, cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            string body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            LogSendFailed(response.StatusCode.ToString(), body);
            response.EnsureSuccessStatusCode();
        }

        LogMessageSent(message.Type, message.Stream ?? "direct");
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Zulip message sent (type={Type}, target={Target})")]
    private partial void LogMessageSent(string type, string target);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Zulip API error: {StatusCode} — {Body}")]
    private partial void LogSendFailed(string statusCode, string body);
}
