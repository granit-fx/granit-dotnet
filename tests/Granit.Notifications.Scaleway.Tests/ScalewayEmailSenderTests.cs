using System.Net;
using System.Text.Json;
using Granit.Notifications.Email;
using Granit.Notifications.Scaleway.Internal;
using Granit.Notifications.Scaleway.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Notifications.Scaleway.Tests;

public sealed class ScalewayEmailSenderTests
{
    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static ScalewayEmailOptions DefaultOptions() => new()
    {
        SecretKey = "scw-test-secret-key",
        ProjectId = "project-123",
        DefaultSenderEmail = "noreply@example.com",
        DefaultSenderName = "Test App",
        Region = "fr-par",
        BaseUrl = "https://api.scaleway.com/transactional-email/v1alpha1",
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

    private static (ScalewayEmailSender Sender, MockHttpMessageHandler Handler) CreateSender(
        ScalewayEmailOptions? options = null,
        HttpStatusCode statusCode = HttpStatusCode.OK,
        string? responseBody = null)
    {
        ScalewayEmailOptions opts = options ?? DefaultOptions();
        var handler = new MockHttpMessageHandler(statusCode, responseBody ?? string.Empty);
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.scaleway.com/transactional-email/v1alpha1/regions/fr-par/"),
        };

        IHttpClientFactory factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient("Scaleway").Returns(httpClient);

        IOptionsMonitor<ScalewayEmailOptions> monitor = Substitute.For<IOptionsMonitor<ScalewayEmailOptions>>();
        monitor.CurrentValue.Returns(opts);

        ScalewayEmailSender sender = new(factory, monitor, NullLogger<ScalewayEmailSender>.Instance);
        return (sender, handler);
    }

    // -------------------------------------------------------------------------
    // Class structure
    // -------------------------------------------------------------------------

    [Fact]
    public void Class_Implements_IEmailSender()
    {
        (ScalewayEmailSender sender, _) = CreateSender();
        sender.ShouldBeAssignableTo<IEmailSender>();
    }

    [Fact]
    public void Class_IsSealed() =>
        typeof(ScalewayEmailSender).IsSealed.ShouldBeTrue();

    [Fact]
    public void Class_IsInternal() =>
        typeof(ScalewayEmailSender).IsNotPublic.ShouldBeTrue();

    // -------------------------------------------------------------------------
    // SendAsync — successful send
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SendAsync_PostsToEmailsEndpoint()
    {
        (ScalewayEmailSender sender, MockHttpMessageHandler handler) = CreateSender();

        await sender.SendAsync(SimpleMessage(), TestContext.Current.CancellationToken);

        handler.LastRequest.ShouldNotBeNull();
        handler.LastRequest.RequestUri!.PathAndQuery.ShouldEndWith("emails");
        handler.LastRequest.Method.ShouldBe(HttpMethod.Post);
    }

    [Fact]
    public async Task SendAsync_SendsCorrectJsonPayload_WithSnakeCaseNaming()
    {
        (ScalewayEmailSender sender, MockHttpMessageHandler handler) = CreateSender();

        await sender.SendAsync(SimpleMessage(), TestContext.Current.CancellationToken);

        handler.LastRequest.ShouldNotBeNull();
        string body = await handler.LastRequest.Content!.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using var doc = JsonDocument.Parse(body);
        JsonElement root = doc.RootElement;

        root.GetProperty("to")[0]
            .GetProperty("email").GetString().ShouldBe("recipient@example.com");
        root.GetProperty("from").GetProperty("email").GetString().ShouldBe("noreply@example.com");
        root.GetProperty("from").GetProperty("name").GetString().ShouldBe("Test App");
        root.GetProperty("subject").GetString().ShouldBe("Test subject");
        root.GetProperty("html").GetString().ShouldBe("<p>Hello</p>");
        root.GetProperty("project_id").GetString().ShouldBe("project-123");
    }

    [Fact]
    public async Task SendAsync_UsesSnakeCaseJsonNaming()
    {
        (ScalewayEmailSender sender, MockHttpMessageHandler handler) = CreateSender();

        await sender.SendAsync(SimpleMessage(), TestContext.Current.CancellationToken);

        string body = await handler.LastRequest!.Content!.ReadAsStringAsync(TestContext.Current.CancellationToken);

        body.ShouldContain("project_id");
        body.ShouldNotContain("projectId");
    }

    // -------------------------------------------------------------------------
    // Sender address resolution
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SendAsync_WithFromEmailOverride_UsesOverrideAddress()
    {
        (ScalewayEmailSender sender, MockHttpMessageHandler handler) = CreateSender();

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
        (ScalewayEmailSender sender, MockHttpMessageHandler handler) = CreateSender();

        await sender.SendAsync(SimpleMessage(), TestContext.Current.CancellationToken);

        string body = await handler.LastRequest!.Content!.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using var doc = JsonDocument.Parse(body);
        doc.RootElement.GetProperty("from").GetProperty("email").GetString().ShouldBe("noreply@example.com");
    }

    // -------------------------------------------------------------------------
    // Body construction
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SendAsync_WithPlainTextBody_IncludesTextField()
    {
        (ScalewayEmailSender sender, MockHttpMessageHandler handler) = CreateSender();

        await sender.SendAsync(
            SimpleMessage(plainText: "Hello plain"),
            TestContext.Current.CancellationToken);

        string body = await handler.LastRequest!.Content!.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using var doc = JsonDocument.Parse(body);
        JsonElement root = doc.RootElement;

        root.GetProperty("text").GetString().ShouldBe("Hello plain");
        root.GetProperty("html").GetString().ShouldBe("<p>Hello</p>");
    }

    [Fact]
    public async Task SendAsync_WithNullPlainTextBody_OmitsTextField()
    {
        (ScalewayEmailSender sender, MockHttpMessageHandler handler) = CreateSender();

        await sender.SendAsync(
            SimpleMessage(plainText: null),
            TestContext.Current.CancellationToken);

        string body = await handler.LastRequest!.Content!.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using var doc = JsonDocument.Parse(body);

        doc.RootElement.TryGetProperty("text", out _).ShouldBeFalse();
    }

    // -------------------------------------------------------------------------
    // Custom headers
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SendAsync_WithHeaders_IncludesAdditionalHeadersInPayload()
    {
        (ScalewayEmailSender sender, MockHttpMessageHandler handler) = CreateSender();

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
        JsonElement headers = root.GetProperty("additional_headers");
        headers.GetProperty("List-Unsubscribe").GetString().ShouldBe("<https://example.com/unsub>");
        headers.GetProperty("List-Unsubscribe-Post").GetString().ShouldBe("List-Unsubscribe=One-Click");
    }

    // -------------------------------------------------------------------------
    // Error handling
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SendAsync_OnApiError_ThrowsHttpRequestException()
    {
        (ScalewayEmailSender sender, _) = CreateSender(
            statusCode: HttpStatusCode.BadRequest,
            responseBody: """{"message":"Bad request"}""");

        HttpRequestException ex = await Should.ThrowAsync<HttpRequestException>(
            () => sender.SendAsync(SimpleMessage(), TestContext.Current.CancellationToken));

        ex.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        ex.Message.ShouldContain("emails");
    }

    [Fact]
    public async Task SendAsync_OnUnauthorized_ThrowsHttpRequestException()
    {
        (ScalewayEmailSender sender, _) = CreateSender(
            statusCode: HttpStatusCode.Unauthorized,
            responseBody: """{"message":"Invalid secret key"}""");

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
        ScalewayEmailOptions opts = DefaultOptions();
        var handler = new MockHttpMessageHandler(HttpStatusCode.OK, string.Empty);
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.scaleway.com/transactional-email/v1alpha1/regions/fr-par/"),
        };

        IHttpClientFactory factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient("Scaleway").Returns(httpClient);

        IOptionsMonitor<ScalewayEmailOptions> monitor = Substitute.For<IOptionsMonitor<ScalewayEmailOptions>>();
        monitor.CurrentValue.Returns(opts);

        ILogger<ScalewayEmailSender> loggerSub = Substitute.For<ILogger<ScalewayEmailSender>>();
        loggerSub.IsEnabled(Arg.Any<LogLevel>()).Returns(true);

        ScalewayEmailSender sender = new(factory, monitor, loggerSub);

        await sender.SendAsync(SimpleMessage(), TestContext.Current.CancellationToken);

        loggerSub.ReceivedWithAnyArgs().Log(
            default, default, default(object)!, default, default!);
    }
}
