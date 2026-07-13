// =============================================================================
// Tests - TwilioNotificationProvider
// =============================================================================
// Verifies the unified Twilio provider: SMS and WhatsApp sending via
// Twilio Messaging API, correct endpoint routing, form-encoded payload
// mapping, whatsapp: prefix handling, error handling on non-2xx responses,
// and Twilio error body parsing.
// =============================================================================

using System.Collections.Specialized;
using System.Net;
using System.Web;
using Granit.Notifications.Sms;
using Granit.Notifications.Twilio.Internal;
using Granit.Notifications.Twilio.Options;
using Granit.Notifications.WhatsApp;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Notifications.Twilio.Tests;

public sealed class TwilioNotificationProviderTests : IDisposable
{
    private readonly MockHttpMessageHandler _handler = new();
    private readonly HttpClient _httpClient;
    private readonly IHttpClientFactory _httpClientFactory = Substitute.For<IHttpClientFactory>();
    private readonly TwilioNotificationProvider _provider;

    public TwilioNotificationProviderTests()
    {
        _httpClient = new HttpClient(_handler)
        {
            BaseAddress = new Uri("https://api.twilio.com/"),
        };

        _httpClientFactory.CreateClient("Twilio").Returns(_httpClient);

        IOptionsMonitor<TwilioOptions> optionsMonitor = Substitute.For<IOptionsMonitor<TwilioOptions>>();
        optionsMonitor.CurrentValue.Returns(new TwilioOptions
        {
            AccountSid = "AC_test_sid",
            AuthToken = "test-auth-token",
            DefaultSmsFromNumber = "+15551234567",
            DefaultWhatsAppFromNumber = "+15559876543",
        });

        ILogger<TwilioNotificationProvider> enabledLogger = Substitute.For<ILogger<TwilioNotificationProvider>>();
        enabledLogger.IsEnabled(Arg.Any<LogLevel>()).Returns(true);

        _provider = new TwilioNotificationProvider(
            _httpClientFactory, optionsMonitor, enabledLogger);
    }

    // -------------------------------------------------------------------------
    // SMS
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SendSmsAsync_PostsToCorrectEndpoint()
    {
        ISmsSender smsSender = _provider;

        await smsSender.SendAsync(
            new SmsMessage
            {
                To = "+32470000000",
                Body = "Hello SMS",
            },
            TestContext.Current.CancellationToken);

        _handler.Requests.Count.ShouldBe(1);
        _handler.Requests[0].Method.ShouldBe("POST");
        _handler.Requests[0].Url.ShouldContain("2010-04-01/Accounts/AC_test_sid/Messages.json");
    }

    [Fact]
    public async Task SendSmsAsync_SendsFormEncodedPayload()
    {
        ISmsSender smsSender = _provider;

        await smsSender.SendAsync(
            new SmsMessage
            {
                To = "+32470000000",
                Body = "Hello SMS",
            },
            TestContext.Current.CancellationToken);

        _handler.Requests.Count.ShouldBe(1);
        string body = _handler.Requests[0].Body;
        NameValueCollection parsed = HttpUtility.ParseQueryString(body);
        parsed["From"].ShouldBe("+15551234567");
        parsed["To"].ShouldBe("+32470000000");
        parsed["Body"].ShouldBe("Hello SMS");
    }

    [Fact]
    public async Task SendSmsAsync_UsesSenderIdFromMessage_WhenProvided()
    {
        ISmsSender smsSender = _provider;

        await smsSender.SendAsync(
            new SmsMessage
            {
                To = "+32470000000",
                Body = "Hello SMS",
                SenderId = "+19995551234",
            },
            TestContext.Current.CancellationToken);

        string body = _handler.Requests[0].Body;
        NameValueCollection parsed = HttpUtility.ParseQueryString(body);
        parsed["From"].ShouldBe("+19995551234");
    }

    [Fact]
    public async Task SendSmsAsync_UsesDefaultFromNumber_WhenSenderIdIsNull()
    {
        ISmsSender smsSender = _provider;

        await smsSender.SendAsync(
            new SmsMessage
            {
                To = "+32470000000",
                Body = "Hello SMS",
            },
            TestContext.Current.CancellationToken);

        string body = _handler.Requests[0].Body;
        NameValueCollection parsed = HttpUtility.ParseQueryString(body);
        parsed["From"].ShouldBe("+15551234567");
    }

    [Fact]
    public async Task SendSmsAsync_ThrowsOnNon2xx()
    {
        _handler.ResponseStatusCode = HttpStatusCode.InternalServerError;
        ISmsSender smsSender = _provider;

        Func<Task> act = () => smsSender.SendAsync(
            new SmsMessage
            {
                To = "+32470000000",
                Body = "Hello SMS",
            },
            TestContext.Current.CancellationToken);

        await Should.ThrowAsync<HttpRequestException>(act);
    }

    [Fact]
    public async Task SendSmsAsync_ThrowsOnNon2xx_WithTwilioErrorBody()
    {
        _handler.ResponseStatusCode = HttpStatusCode.BadRequest;
        _handler.ResponseBody = """{"code":21211,"message":"Invalid 'To' Phone Number"}""";
        ISmsSender smsSender = _provider;

        HttpRequestException ex = await Should.ThrowAsync<HttpRequestException>(() => smsSender.SendAsync(
            new SmsMessage
            {
                To = "bad-number",
                Body = "Hello SMS",
            },
            TestContext.Current.CancellationToken));

        // The vendor error body may echo the recipient — it must never reach the
        // exception message (it is logged scrubbed instead).
        ex.Message.ShouldNotContain("Invalid 'To' Phone Number");
        ex.Message.ShouldContain("Twilio API error 400");
        ex.Message.ShouldContain("Messages.json");
        ex.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task SendSmsAsync_ThrowsOnNon2xx_WithNoBody()
    {
        _handler.ResponseStatusCode = HttpStatusCode.InternalServerError;
        _handler.ResponseBody = null;
        ISmsSender smsSender = _provider;

        HttpRequestException ex = await Should.ThrowAsync<HttpRequestException>(() => smsSender.SendAsync(
            new SmsMessage
            {
                To = "+32470000000",
                Body = "Hello SMS",
            },
            TestContext.Current.CancellationToken));

        ex.Message.ShouldContain("Messages.json");
        ex.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
    }

    // -------------------------------------------------------------------------
    // WhatsApp
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SendWhatsAppAsync_PostsToCorrectEndpoint()
    {
        IWhatsAppSender whatsAppSender = _provider;

        await whatsAppSender.SendAsync(
            new WhatsAppMessage
            {
                To = "+32470000000",
                TemplateName = "welcome_template",
            },
            TestContext.Current.CancellationToken);

        _handler.Requests.Count.ShouldBe(1);
        _handler.Requests[0].Method.ShouldBe("POST");
        _handler.Requests[0].Url.ShouldContain("2010-04-01/Accounts/AC_test_sid/Messages.json");
    }

    [Fact]
    public async Task SendWhatsAppAsync_SendsFormEncodedPayload_WithWhatsAppPrefix()
    {
        IWhatsAppSender whatsAppSender = _provider;

        await whatsAppSender.SendAsync(
            new WhatsAppMessage
            {
                To = "+32470000000",
                TemplateName = "welcome_template",
            },
            TestContext.Current.CancellationToken);

        _handler.Requests.Count.ShouldBe(1);
        string body = _handler.Requests[0].Body;
        NameValueCollection parsed = HttpUtility.ParseQueryString(body);
        parsed["From"].ShouldBe("whatsapp:+15559876543");
        parsed["To"].ShouldBe("whatsapp:+32470000000");
        parsed["Body"].ShouldBe("welcome_template");
    }

    [Fact]
    public async Task SendWhatsAppAsync_FallsBackToSmsNumber_WhenWhatsAppNumberIsNull()
    {
        IOptionsMonitor<TwilioOptions> optionsMonitor = Substitute.For<IOptionsMonitor<TwilioOptions>>();
        optionsMonitor.CurrentValue.Returns(new TwilioOptions
        {
            AccountSid = "AC_test_sid",
            AuthToken = "test-auth-token",
            DefaultSmsFromNumber = "+15551234567",
            DefaultWhatsAppFromNumber = null,
        });

        ILogger<TwilioNotificationProvider> logger = Substitute.For<ILogger<TwilioNotificationProvider>>();
        logger.IsEnabled(Arg.Any<LogLevel>()).Returns(true);

        TwilioNotificationProvider provider = new(_httpClientFactory, optionsMonitor, logger);
        IWhatsAppSender whatsAppSender = provider;

        await whatsAppSender.SendAsync(
            new WhatsAppMessage
            {
                To = "+32470000000",
                TemplateName = "welcome_template",
            },
            TestContext.Current.CancellationToken);

        string body = _handler.Requests[0].Body;
        NameValueCollection parsed = HttpUtility.ParseQueryString(body);
        parsed["From"].ShouldBe("whatsapp:+15551234567");
    }

    [Fact]
    public async Task SendWhatsAppAsync_IncludesTemplateParameters_InBody()
    {
        IWhatsAppSender whatsAppSender = _provider;

        await whatsAppSender.SendAsync(
            new WhatsAppMessage
            {
                To = "+32470000000",
                TemplateName = "welcome_template",
                TemplateParameters = ["Jean", "2026"],
            },
            TestContext.Current.CancellationToken);

        string body = _handler.Requests[0].Body;
        NameValueCollection parsed = HttpUtility.ParseQueryString(body);
        string? bodyValue = parsed["Body"];
        bodyValue.ShouldNotBeNull();
        bodyValue.ShouldContain("welcome_template");
        bodyValue.ShouldContain("Jean");
        bodyValue.ShouldContain("2026");
    }

    [Fact]
    public async Task SendWhatsAppAsync_WithEmptyTemplateParameters_SendsTemplateNameOnly()
    {
        IWhatsAppSender whatsAppSender = _provider;

        await whatsAppSender.SendAsync(
            new WhatsAppMessage
            {
                To = "+32470000000",
                TemplateName = "simple_template",
                TemplateParameters = [],
            },
            TestContext.Current.CancellationToken);

        string body = _handler.Requests[0].Body;
        NameValueCollection parsed = HttpUtility.ParseQueryString(body);
        parsed["Body"].ShouldBe("simple_template");
    }

    [Fact]
    public async Task SendWhatsAppAsync_ThrowsOnNon2xx()
    {
        _handler.ResponseStatusCode = HttpStatusCode.Forbidden;
        _handler.ResponseBody = """{"code":20003,"message":"Access denied"}""";
        IWhatsAppSender whatsAppSender = _provider;

        HttpRequestException ex = await Should.ThrowAsync<HttpRequestException>(() => whatsAppSender.SendAsync(
            new WhatsAppMessage
            {
                To = "+32470000000",
                TemplateName = "welcome_template",
            },
            TestContext.Current.CancellationToken));

        ex.Message.ShouldNotContain("Access denied");
        ex.Message.ShouldContain("Twilio API error");
        ex.Message.ShouldContain("Messages.json");
        ex.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // -------------------------------------------------------------------------
    // EnsureSuccessAsync -- error body parsing
    // -------------------------------------------------------------------------

    [Fact]
    public async Task EnsureSuccessAsync_ThrowsWithNoBody_WhenContentReadThrows()
    {
        ThrowOnReadHttpMessageHandler throwingHandler = new(HttpStatusCode.InternalServerError);
        using HttpClient throwingClient = new(throwingHandler)
        {
            BaseAddress = new Uri("https://api.twilio.com/"),
        };

        IHttpClientFactory factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient("Twilio").Returns(throwingClient);

        IOptionsMonitor<TwilioOptions> optionsMonitor = Substitute.For<IOptionsMonitor<TwilioOptions>>();
        optionsMonitor.CurrentValue.Returns(new TwilioOptions
        {
            AccountSid = "AC_test_sid",
            AuthToken = "test-auth-token",
            DefaultSmsFromNumber = "+15551234567",
        });

        ILogger<TwilioNotificationProvider> throwTestLogger = Substitute.For<ILogger<TwilioNotificationProvider>>();
        throwTestLogger.IsEnabled(Arg.Any<LogLevel>()).Returns(true);

        TwilioNotificationProvider provider = new(
            factory, optionsMonitor, throwTestLogger);
        ISmsSender smsSender = provider;

        await Should.ThrowAsync<Exception>(() => smsSender.SendAsync(
            new SmsMessage
            {
                To = "+32470000000",
                Body = "Hello SMS",
            },
            TestContext.Current.CancellationToken));
    }

    // -------------------------------------------------------------------------
    // Class structure
    // -------------------------------------------------------------------------

    [Fact]
    public void Class_IsSealed() =>
        typeof(TwilioNotificationProvider).IsSealed.ShouldBeTrue();

    [Fact]
    public void Class_IsInternal() =>
        typeof(TwilioNotificationProvider).IsNotPublic.ShouldBeTrue();

    [Fact]
    public void Class_ImplementsISmsSender() =>
        _provider.ShouldBeAssignableTo<ISmsSender>();

    [Fact]
    public void Class_ImplementsIWhatsAppSender() =>
        _provider.ShouldBeAssignableTo<IWhatsAppSender>();

    // -------------------------------------------------------------------------
    // IDisposable
    // -------------------------------------------------------------------------

    /// <inheritdoc />
    public void Dispose()
    {
        _httpClient.Dispose();
        _handler.Dispose();
    }
}
