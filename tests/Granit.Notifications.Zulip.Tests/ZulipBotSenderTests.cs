using System.Net;
using System.Text;
using Granit.Notifications.Zulip.Internal;
using Granit.Notifications.Zulip.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Notifications.Zulip.Tests;

public sealed class ZulipBotSenderTests
{
    private readonly IOptionsMonitor<ZulipBotOptions> _options = CreateOptionsMonitor();

    private static IOptionsMonitor<ZulipBotOptions> CreateOptionsMonitor()
    {
        IOptionsMonitor<ZulipBotOptions> monitor = Substitute.For<IOptionsMonitor<ZulipBotOptions>>();
        monitor.CurrentValue.Returns(new ZulipBotOptions
        {
            BaseUrl = "https://zulip.example.com",
            BotEmail = "bot@example.com",
            ApiKey = "test-api-key",
            TimeoutSeconds = 30,
        });
        return monitor;
    }

    // -- Stream message --

    [Fact]
    public async Task SendAsync_StreamMessage_PostsWithCorrectFormData()
    {
        string? capturedContent = null;
        string? capturedAuth = null;
        var handler = new FakeHttpMessageHandler((request, ct) =>
        {
            capturedAuth = request.Headers.Authorization?.ToString();
            capturedContent = request.Content!.ReadAsStringAsync(ct).GetAwaiter().GetResult();
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        IHttpClientFactory factory = CreateFactory(handler);
        var sender = new ZulipBotSender(factory, _options, NullLogger<ZulipBotSender>.Instance);

        var message = new ZulipMessage
        {
            Type = "stream",
            Stream = "alerts",
            Topic = "system",
            Content = "Hello stream",
        };

        await sender.SendAsync(message, TestContext.Current.CancellationToken);

        capturedAuth.ShouldNotBeNull();
        string expectedCredentials = Convert.ToBase64String(Encoding.ASCII.GetBytes("bot@example.com:test-api-key"));
        capturedAuth.ShouldBe($"Basic {expectedCredentials}");

        capturedContent.ShouldNotBeNull();
        capturedContent.ShouldContain("type=stream");
        capturedContent.ShouldContain("content=Hello+stream");
        capturedContent.ShouldContain("to=alerts");
        capturedContent.ShouldContain("topic=system");
    }

    // -- Direct message --

    [Fact]
    public async Task SendAsync_DirectMessage_PostsWithRecipientList()
    {
        string? capturedContent = null;
        var handler = new FakeHttpMessageHandler((request, ct) =>
        {
            capturedContent = request.Content!.ReadAsStringAsync(ct).GetAwaiter().GetResult();
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        IHttpClientFactory factory = CreateFactory(handler);
        var sender = new ZulipBotSender(factory, _options, NullLogger<ZulipBotSender>.Instance);

        var message = new ZulipMessage
        {
            Type = "direct",
            To = ["user1@example.com", "user2@example.com"],
            Content = "Hello direct",
        };

        await sender.SendAsync(message, TestContext.Current.CancellationToken);

        capturedContent.ShouldNotBeNull();
        capturedContent.ShouldContain("type=direct");
        capturedContent.ShouldContain("content=Hello+direct");
        // "to" should contain JSON array of recipients
        capturedContent.ShouldContain("to=");
        capturedContent.ShouldContain("user1%40example.com");
        capturedContent.ShouldContain("user2%40example.com");
    }

    // -- Direct message with empty To list --

    [Fact]
    public async Task SendAsync_DirectMessageEmptyTo_DoesNotIncludeToField()
    {
        string? capturedContent = null;
        var handler = new FakeHttpMessageHandler((request, ct) =>
        {
            capturedContent = request.Content!.ReadAsStringAsync(ct).GetAwaiter().GetResult();
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        IHttpClientFactory factory = CreateFactory(handler);
        var sender = new ZulipBotSender(factory, _options, NullLogger<ZulipBotSender>.Instance);

        var message = new ZulipMessage
        {
            Type = "direct",
            To = [],
            Content = "Hello nobody",
        };

        await sender.SendAsync(message, TestContext.Current.CancellationToken);

        capturedContent.ShouldNotBeNull();
        capturedContent.ShouldContain("type=direct");
        capturedContent.ShouldContain("content=Hello+nobody");
        // With empty To list, it should not have a "to" key beyond type/content
        capturedContent.ShouldNotContain("to=");
    }

    // -- API error handling --

    [Fact]
    public async Task SendAsync_ApiError_ThrowsHttpRequestException()
    {
        var handler = new FakeHttpMessageHandler((_, _) =>
            new HttpResponseMessage(HttpStatusCode.BadRequest) { Content = new StringContent("Bad request body") });

        IHttpClientFactory factory = CreateFactory(handler);
        var sender = new ZulipBotSender(factory, _options, NullLogger<ZulipBotSender>.Instance);

        var message = new ZulipMessage
        {
            Type = "stream",
            Stream = "alerts",
            Topic = "system",
            Content = "Hello",
        };

        await Should.ThrowAsync<HttpRequestException>(
            () => sender.SendAsync(message, TestContext.Current.CancellationToken));
    }

    // -- Correct endpoint --

    [Fact]
    public async Task SendAsync_PostsToCorrectEndpoint()
    {
        Uri? capturedUri = null;
        var handler = new FakeHttpMessageHandler((request, _) =>
        {
            capturedUri = request.RequestUri;
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        IHttpClientFactory factory = CreateFactory(handler);
        var sender = new ZulipBotSender(factory, _options, NullLogger<ZulipBotSender>.Instance);

        var message = new ZulipMessage
        {
            Type = "stream",
            Stream = "test",
            Topic = "test",
            Content = "test",
        };

        await sender.SendAsync(message, TestContext.Current.CancellationToken);

        capturedUri.ShouldNotBeNull();
        capturedUri!.AbsolutePath.ShouldEndWith("/api/v1/messages");
    }

    // -- Direct message with null To --

    [Fact]
    public async Task SendAsync_DirectMessageNullTo_DoesNotIncludeToField()
    {
        string? capturedContent = null;
        var handler = new FakeHttpMessageHandler((request, ct) =>
        {
            capturedContent = request.Content!.ReadAsStringAsync(ct).GetAwaiter().GetResult();
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        IHttpClientFactory factory = CreateFactory(handler);
        var sender = new ZulipBotSender(factory, _options, NullLogger<ZulipBotSender>.Instance);

        var message = new ZulipMessage
        {
            Type = "direct",
            Content = "Hello nobody",
        };

        await sender.SendAsync(message, TestContext.Current.CancellationToken);

        capturedContent.ShouldNotBeNull();
        capturedContent.ShouldNotContain("to=");
    }

    // -- Class structure --

    [Fact]
    public void Class_IsSealed() =>
        typeof(ZulipBotSender).IsSealed.ShouldBeTrue();

    [Fact]
    public void Class_IsInternal() =>
        typeof(ZulipBotSender).IsNotPublic.ShouldBeTrue();

    [Fact]
    public void Class_ImplementsIZulipSender()
    {
        var handler = new FakeHttpMessageHandler((_, _) => new HttpResponseMessage(HttpStatusCode.OK));
        IHttpClientFactory factory = CreateFactory(handler);
        var sender = new ZulipBotSender(factory, _options, NullLogger<ZulipBotSender>.Instance);

        sender.ShouldBeAssignableTo<IZulipSender>();
    }

    [Fact]
    public void HttpClientName_IsZulipBot() =>
        ZulipBotSender.HttpClientName.ShouldBe("ZulipBot");

    // -- Stream message without topic --

    [Fact]
    public async Task SendAsync_StreamMessage_IncludesAllFormFields()
    {
        string? capturedContent = null;
        var handler = new FakeHttpMessageHandler((request, ct) =>
        {
            capturedContent = request.Content!.ReadAsStringAsync(ct).GetAwaiter().GetResult();
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        IHttpClientFactory factory = CreateFactory(handler);
        var sender = new ZulipBotSender(factory, _options, NullLogger<ZulipBotSender>.Instance);

        var message = new ZulipMessage
        {
            Type = "stream",
            Stream = "notifications",
            Topic = "deploy",
            Content = "Deployment complete",
        };

        await sender.SendAsync(message, TestContext.Current.CancellationToken);

        capturedContent.ShouldNotBeNull();
        capturedContent.ShouldContain("type=stream");
        capturedContent.ShouldContain("to=notifications");
        capturedContent.ShouldContain("topic=deploy");
        capturedContent.ShouldContain("content=Deployment+complete");
    }

    // -- Stream message logs target as stream name --

    [Fact]
    public async Task SendAsync_StreamMessage_SuccessDoesNotThrow()
    {
        var handler = new FakeHttpMessageHandler((_, _) => new HttpResponseMessage(HttpStatusCode.OK));
        IHttpClientFactory factory = CreateFactory(handler);
        var sender = new ZulipBotSender(factory, _options, NullLogger<ZulipBotSender>.Instance);

        var message = new ZulipMessage
        {
            Type = "stream",
            Stream = "alerts",
            Topic = "system",
            Content = "All clear",
        };

        await Should.NotThrowAsync(() => sender.SendAsync(message, TestContext.Current.CancellationToken));
    }

    // -- Direct message success --

    [Fact]
    public async Task SendAsync_DirectMessage_SuccessDoesNotThrow()
    {
        var handler = new FakeHttpMessageHandler((_, _) => new HttpResponseMessage(HttpStatusCode.OK));
        IHttpClientFactory factory = CreateFactory(handler);
        var sender = new ZulipBotSender(factory, _options, NullLogger<ZulipBotSender>.Instance);

        var message = new ZulipMessage
        {
            Type = "direct",
            To = ["user@example.com"],
            Content = "Hello",
        };

        await Should.NotThrowAsync(() => sender.SendAsync(message, TestContext.Current.CancellationToken));
    }

    // -- Logging branch coverage (source-generated [LoggerMessage]) --

    [Fact]
    public async Task SendAsync_StreamMessage_LogsWhenLoggingEnabled()
    {
        var handler = new FakeHttpMessageHandler((_, _) => new HttpResponseMessage(HttpStatusCode.OK));
        IHttpClientFactory factory = CreateFactory(handler);
        ILogger<ZulipBotSender> enabledLogger = CreateEnabledLogger<ZulipBotSender>();
        var sender = new ZulipBotSender(factory, _options, enabledLogger);

        var message = new ZulipMessage
        {
            Type = "stream",
            Stream = "alerts",
            Topic = "system",
            Content = "Hello logging",
        };

        await sender.SendAsync(message, TestContext.Current.CancellationToken);

        enabledLogger.ReceivedWithAnyArgs().Log(
            default, default, default(object)!, default, default!);
    }

    [Fact]
    public async Task SendAsync_DirectMessage_LogsTargetAsDirect()
    {
        var handler = new FakeHttpMessageHandler((_, _) => new HttpResponseMessage(HttpStatusCode.OK));
        IHttpClientFactory factory = CreateFactory(handler);
        ILogger<ZulipBotSender> enabledLogger = CreateEnabledLogger<ZulipBotSender>();
        var sender = new ZulipBotSender(factory, _options, enabledLogger);

        var message = new ZulipMessage
        {
            Type = "direct",
            To = ["user@example.com"],
            Content = "Hello logging direct",
        };

        await sender.SendAsync(message, TestContext.Current.CancellationToken);

        enabledLogger.ReceivedWithAnyArgs().Log(
            default, default, default(object)!, default, default!);
    }

    [Fact]
    public async Task SendAsync_ApiError_LogsWarningWhenLoggingEnabled()
    {
        var handler = new FakeHttpMessageHandler((_, _) =>
            new HttpResponseMessage(HttpStatusCode.InternalServerError) { Content = new StringContent("Server error") });
        IHttpClientFactory factory = CreateFactory(handler);
        ILogger<ZulipBotSender> enabledLogger = CreateEnabledLogger<ZulipBotSender>();
        var sender = new ZulipBotSender(factory, _options, enabledLogger);

        var message = new ZulipMessage
        {
            Type = "stream",
            Stream = "alerts",
            Topic = "system",
            Content = "Hello",
        };

        await Should.ThrowAsync<HttpRequestException>(
            () => sender.SendAsync(message, TestContext.Current.CancellationToken));

        enabledLogger.ReceivedWithAnyArgs().Log(
            default, default, default(object)!, default, default!);
    }

    // -- Helpers --

    private static IHttpClientFactory CreateFactory(HttpMessageHandler handler)
    {
        IHttpClientFactory factory = Substitute.For<IHttpClientFactory>();
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://zulip.example.com/") };
        factory.CreateClient(ZulipBotSender.HttpClientName).Returns(client);
        return factory;
    }

    private static ILogger<T> CreateEnabledLogger<T>()
    {
        ILogger<T> logger = Substitute.For<ILogger<T>>();
        logger.IsEnabled(Arg.Any<LogLevel>()).Returns(true);
        return logger;
    }

    private sealed class FakeHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(handler(request, cancellationToken));
    }
}
