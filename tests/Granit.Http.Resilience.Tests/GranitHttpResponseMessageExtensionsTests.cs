using System.Net;
using System.Text;
using Granit.Http.Resilience.Extensions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Http.Resilience.Tests;

public sealed class GranitHttpResponseMessageExtensionsTests
{
    private readonly ILogger _logger = Substitute.For<ILogger>();

    [Fact]
    public async Task SuccessResponse_DoesNothing()
    {
        using HttpResponseMessage response = new(HttpStatusCode.OK);

        await Should.NotThrowAsync(() => response.EnsureGranitSuccessAsync(
            _logger, "SendGrid", cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ErrorResponse_ThrowsWithStatusAndProvider_ButNeverTheBody()
    {
        using HttpResponseMessage response = new(HttpStatusCode.BadRequest)
        {
            Content = new StringContent("""{"error":"invalid recipient john.doe@example.com"}""", Encoding.UTF8),
        };

        HttpRequestException ex = await Should.ThrowAsync<HttpRequestException>(
            () => response.EnsureGranitSuccessAsync(
                _logger, "SendGrid", "/v3/mail/send", TestContext.Current.CancellationToken));

        ex.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        ex.Message.ShouldContain("SendGrid API error 400 on /v3/mail/send");
        // The body may echo the recipient — it must never travel in the exception message
        // (exception messages end up in delivery audit rows and upstream logs).
        ex.Message.ShouldNotContain("john.doe@example.com");
        ex.Message.ShouldNotContain("invalid recipient");
    }

    [Fact]
    public async Task ErrorResponse_LogsScrubbedBodyAtWarning()
    {
        using HttpResponseMessage response = new(HttpStatusCode.UnprocessableEntity)
        {
            Content = new StringContent("""{"to":"jane.doe@customer.org","detail":"blocked"}""", Encoding.UTF8),
        };

        RecordingLogger recorder = new();

        await Should.ThrowAsync<HttpRequestException>(
            () => response.EnsureGranitSuccessAsync(
                recorder, "Brevo", cancellationToken: TestContext.Current.CancellationToken));

        (LogLevel level, string message) = recorder.Entries.ShouldHaveSingleItem();
        level.ShouldBe(LogLevel.Warning);
        message.ShouldNotContain("jane.doe@customer.org"); // scrubbed
        message.ShouldContain("@customer.org");            // domain kept for debugging
        message.ShouldContain("blocked");                  // diagnostic content preserved
    }

    /// <summary>[LoggerMessage] guards on IsEnabled, so a plain substitute records nothing.</summary>
    private sealed class RecordingLogger : ILogger
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Entries.Add((logLevel, formatter(state, exception)));
    }

    [Fact]
    public async Task ErrorResponse_WithoutEndpoint_OmitsEndpointFromMessage()
    {
        using HttpResponseMessage response = new(HttpStatusCode.InternalServerError);

        HttpRequestException ex = await Should.ThrowAsync<HttpRequestException>(
            () => response.EnsureGranitSuccessAsync(
                _logger, "Zulip", cancellationToken: TestContext.Current.CancellationToken));

        ex.Message.ShouldBe("Zulip API error 500");
    }
}
