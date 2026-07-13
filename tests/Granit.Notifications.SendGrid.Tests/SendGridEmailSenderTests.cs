using System.Net;
using System.Text.Json;
using Granit.Notifications.Email;
using Granit.Notifications.SendGrid.Internal;
using Granit.Notifications.SendGrid.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Notifications.SendGrid.Tests;

public sealed class SendGridEmailSenderTests
{
    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static SendGridEmailOptions DefaultOptions() => new()
    {
        ApiKey = "SG.test-api-key",
        DefaultSenderEmail = "noreply@example.com",
        DefaultSenderName = "Test App",
        BaseUrl = "https://api.sendgrid.com/v3",
        TimeoutSeconds = 10,
    };

    private static EmailMessage SimpleMessage(string? fromOverride = null, string? plainText = null) =>
        new()
        {
            To = "recipient@example.com",
            Subject = "Test subject",
            HtmlBody = "<p>Hello</p>",
            FromEmailOverride = fromOverride,
            PlainTextBody = plainText,
        };

    private static (SendGridEmailSender Sender, MockHttpMessageHandler Handler) CreateSender(
        SendGridEmailOptions? options = null,
        HttpStatusCode statusCode = HttpStatusCode.Accepted,
        string? responseBody = null)
    {
        SendGridEmailOptions opts = options ?? DefaultOptions();
        var handler = new MockHttpMessageHandler(statusCode, responseBody ?? string.Empty);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.sendgrid.com/v3/") };

        IHttpClientFactory factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient("SendGrid").Returns(httpClient);

        IOptionsMonitor<SendGridEmailOptions> monitor = Substitute.For<IOptionsMonitor<SendGridEmailOptions>>();
        monitor.CurrentValue.Returns(opts);

        SendGridEmailSender sender = new(factory, monitor, NullLogger<SendGridEmailSender>.Instance);
        return (sender, handler);
    }

    // -------------------------------------------------------------------------
    // Class structure
    // -------------------------------------------------------------------------

    [Fact]
    public void Class_Implements_IEmailSender()
    {
        (SendGridEmailSender sender, _) = CreateSender();
        sender.ShouldBeAssignableTo<IEmailSender>();
    }

    [Fact]
    public void Class_IsSealed() =>
        typeof(SendGridEmailSender).IsSealed.ShouldBeTrue();

    [Fact]
    public void Class_IsInternal() =>
        typeof(SendGridEmailSender).IsNotPublic.ShouldBeTrue();

    // -------------------------------------------------------------------------
    // SendAsync — successful send
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SendAsync_PostsToMailSendEndpoint()
    {
        (SendGridEmailSender sender, MockHttpMessageHandler handler) = CreateSender();

        await sender.SendAsync(SimpleMessage(), TestContext.Current.CancellationToken);

        handler.LastRequest.ShouldNotBeNull();
        handler.LastRequest.RequestUri!.PathAndQuery.ShouldEndWith("mail/send");
        handler.LastRequest.Method.ShouldBe(HttpMethod.Post);
    }

    [Fact]
    public async Task SendAsync_SendsCorrectJsonPayload()
    {
        (SendGridEmailSender sender, MockHttpMessageHandler handler) = CreateSender();

        await sender.SendAsync(SimpleMessage(), TestContext.Current.CancellationToken);

        handler.LastRequest.ShouldNotBeNull();
        string body = await handler.LastRequest.Content!.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using var doc = JsonDocument.Parse(body);
        JsonElement root = doc.RootElement;

        root.GetProperty("personalizations")[0]
            .GetProperty("to")[0]
            .GetProperty("email").GetString().ShouldBe("recipient@example.com");
        root.GetProperty("from").GetProperty("email").GetString().ShouldBe("noreply@example.com");
        root.GetProperty("from").GetProperty("name").GetString().ShouldBe("Test App");
        root.GetProperty("subject").GetString().ShouldBe("Test subject");
        root.GetProperty("content")[0].GetProperty("type").GetString().ShouldBe("text/html");
        root.GetProperty("content")[0].GetProperty("value").GetString().ShouldBe("<p>Hello</p>");
    }

    // -------------------------------------------------------------------------
    // Sender address resolution
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SendAsync_WithFromEmailOverride_UsesOverrideAddress()
    {
        (SendGridEmailSender sender, MockHttpMessageHandler handler) = CreateSender();

        await sender.SendAsync(
            SimpleMessage(fromOverride: "custom@example.com"),
            TestContext.Current.CancellationToken);

        string body = await handler.LastRequest!.Content!.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using var doc = JsonDocument.Parse(body);
        doc.RootElement.GetProperty("from").GetProperty("email").GetString().ShouldBe("custom@example.com");
    }

    [Fact]
    public async Task SendAsync_WithoutFromEmailOverride_UsesDefaultSenderEmail()
    {
        (SendGridEmailSender sender, MockHttpMessageHandler handler) = CreateSender();

        await sender.SendAsync(SimpleMessage(), TestContext.Current.CancellationToken);

        string body = await handler.LastRequest!.Content!.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using var doc = JsonDocument.Parse(body);
        doc.RootElement.GetProperty("from").GetProperty("email").GetString().ShouldBe("noreply@example.com");
    }

    // -------------------------------------------------------------------------
    // Body construction
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SendAsync_WithPlainTextBody_IncludesPlainTextContent()
    {
        (SendGridEmailSender sender, MockHttpMessageHandler handler) = CreateSender();

        await sender.SendAsync(
            SimpleMessage(plainText: "Hello plain"),
            TestContext.Current.CancellationToken);

        string body = await handler.LastRequest!.Content!.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using var doc = JsonDocument.Parse(body);
        JsonElement contentArray = doc.RootElement.GetProperty("content");

        contentArray.GetArrayLength().ShouldBe(2);
        contentArray[0].GetProperty("type").GetString().ShouldBe("text/plain");
        contentArray[0].GetProperty("value").GetString().ShouldBe("Hello plain");
        contentArray[1].GetProperty("type").GetString().ShouldBe("text/html");
        contentArray[1].GetProperty("value").GetString().ShouldBe("<p>Hello</p>");
    }

    [Fact]
    public async Task SendAsync_WithNullPlainTextBody_OnlyIncludesHtmlContent()
    {
        (SendGridEmailSender sender, MockHttpMessageHandler handler) = CreateSender();

        await sender.SendAsync(
            SimpleMessage(plainText: null),
            TestContext.Current.CancellationToken);

        string body = await handler.LastRequest!.Content!.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using var doc = JsonDocument.Parse(body);
        JsonElement contentArray = doc.RootElement.GetProperty("content");

        contentArray.GetArrayLength().ShouldBe(1);
        contentArray[0].GetProperty("type").GetString().ShouldBe("text/html");
    }

    // -------------------------------------------------------------------------
    // Custom headers
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SendAsync_WithHeaders_IncludesHeadersInPayload()
    {
        (SendGridEmailSender sender, MockHttpMessageHandler handler) = CreateSender();

        EmailMessage message = new()
        {
            To = "recipient@example.com",
            Subject = "Test subject",
            HtmlBody = "<p>Hello</p>",
            Headers = new Dictionary<string, string>
            {
                ["List-Unsubscribe"] = "<https://example.com/unsub>",
                ["List-Unsubscribe-Post"] = "List-Unsubscribe=One-Click",
            },
        };

        await sender.SendAsync(message, TestContext.Current.CancellationToken);

        handler.LastRequest.ShouldNotBeNull();
        string body = await handler.LastRequest.Content!.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using var doc = JsonDocument.Parse(body);
        JsonElement root = doc.RootElement;
        JsonElement headers = root.GetProperty("headers");
        headers.GetProperty("List-Unsubscribe").GetString().ShouldBe("<https://example.com/unsub>");
        headers.GetProperty("List-Unsubscribe-Post").GetString().ShouldBe("List-Unsubscribe=One-Click");
    }

    // -------------------------------------------------------------------------
    // Error handling
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SendAsync_OnApiError_ThrowsHttpRequestException()
    {
        (SendGridEmailSender sender, _) = CreateSender(
            statusCode: HttpStatusCode.BadRequest,
            responseBody: """{"errors":[{"message":"Bad request"}]}""");

        HttpRequestException ex = await Should.ThrowAsync<HttpRequestException>(
            () => sender.SendAsync(SimpleMessage(), TestContext.Current.CancellationToken));

        ex.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        ex.Message.ShouldContain("mail/send");
    }

    [Fact]
    public async Task SendAsync_OnUnauthorized_ThrowsHttpRequestException()
    {
        (SendGridEmailSender sender, _) = CreateSender(
            statusCode: HttpStatusCode.Unauthorized,
            responseBody: """{"errors":[{"message":"Invalid API key"}]}""");

        HttpRequestException ex = await Should.ThrowAsync<HttpRequestException>(
            () => sender.SendAsync(SimpleMessage(), TestContext.Current.CancellationToken));

        ex.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    // -------------------------------------------------------------------------
    // Logger
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SendAsync_LogsEmailSent()
    {
        SendGridEmailOptions opts = DefaultOptions();
        var handler = new MockHttpMessageHandler(HttpStatusCode.Accepted, string.Empty);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.sendgrid.com/v3/") };

        IHttpClientFactory factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient("SendGrid").Returns(httpClient);

        IOptionsMonitor<SendGridEmailOptions> monitor = Substitute.For<IOptionsMonitor<SendGridEmailOptions>>();
        monitor.CurrentValue.Returns(opts);

        ILogger<SendGridEmailSender> loggerSub = Substitute.For<ILogger<SendGridEmailSender>>();
        loggerSub.IsEnabled(Arg.Any<LogLevel>()).Returns(true);

        SendGridEmailSender sender = new(factory, monitor, loggerSub);

        await sender.SendAsync(SimpleMessage(), TestContext.Current.CancellationToken);

        loggerSub.ReceivedWithAnyArgs().Log(
            default, default, default(object)!, default, default!);
    }

    // -------------------------------------------------------------------------
    // Mock handler
    // -------------------------------------------------------------------------

    internal sealed class MockHttpMessageHandler(HttpStatusCode statusCode, string responseBody) : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(responseBody),
            });
        }
    }
}
