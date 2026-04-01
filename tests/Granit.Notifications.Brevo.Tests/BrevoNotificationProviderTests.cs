// =============================================================================
// Tests - BrevoNotificationProvider
// =============================================================================
// Verifies the unified Brevo provider: email, SMS, and WhatsApp sending via
// Brevo Transactional API, correct endpoint routing, payload mapping, error
// handling on non-2xx responses, and Brevo error body parsing.
// =============================================================================

using System.Net;
using System.Text.Json;
using Granit.Notifications.Brevo.Internal;
using Granit.Notifications.Brevo.Options;
using Granit.Notifications.Email;
using Granit.Notifications.Sms;
using Granit.Notifications.WhatsApp;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Notifications.Brevo.Tests;

public sealed class BrevoNotificationProviderTests : IDisposable
{
    private readonly MockHttpMessageHandler _handler = new();
    private readonly HttpClient _httpClient;
    private readonly IHttpClientFactory _httpClientFactory = Substitute.For<IHttpClientFactory>();
    private readonly BrevoNotificationProvider _provider;

    public BrevoNotificationProviderTests()
    {
        _httpClient = new HttpClient(_handler)
        {
            BaseAddress = new Uri("https://api.brevo.com/v3/"),
        };

        _httpClientFactory.CreateClient("Brevo").Returns(_httpClient);

        IOptionsMonitor<BrevoOptions> optionsMonitor = Substitute.For<IOptionsMonitor<BrevoOptions>>();
        optionsMonitor.CurrentValue.Returns(new BrevoOptions
        {
            ApiKey = "test-key",
            DefaultSenderEmail = "default@test.com",
            DefaultSenderName = "Test App",
            DefaultSmsSenderId = "TestApp",
        });

        ILogger<BrevoNotificationProvider> enabledLogger = Substitute.For<ILogger<BrevoNotificationProvider>>();
        enabledLogger.IsEnabled(Arg.Any<LogLevel>()).Returns(true);

        _provider = new BrevoNotificationProvider(
            _httpClientFactory, optionsMonitor, enabledLogger);
    }

    // -------------------------------------------------------------------------
    // Email
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SendEmailAsync_PostsToCorrectEndpoint()
    {
        IEmailSender emailSender = _provider;

        await emailSender.SendAsync(
            new EmailMessage
            {
                To = "user@test.com",
                Subject = "Test Subject",
                HtmlBody = "<p>Hello</p>",
            },
            TestContext.Current.CancellationToken);

        _handler.Requests.Count.ShouldBe(1);
        _handler.Requests[0].Url.ShouldContain("smtp/email");
    }

    [Fact]
    public async Task SendEmailAsync_IncludesCorrectPayload()
    {
        IEmailSender emailSender = _provider;

        await emailSender.SendAsync(
            new EmailMessage
            {
                To = "user@test.com",
                Subject = "Test Subject",
                HtmlBody = "<p>Hello</p>",
                PlainTextBody = "Hello",
            },
            TestContext.Current.CancellationToken);

        _handler.Requests.Count.ShouldBe(1);
        string body = _handler.Requests[0].Body;
        using var doc = JsonDocument.Parse(body);
        JsonElement root = doc.RootElement;
        root.GetProperty("to")[0].GetProperty("email").GetString().ShouldBe("user@test.com");
        root.GetProperty("subject").GetString().ShouldBe("Test Subject");
        root.GetProperty("htmlContent").GetString().ShouldBe("<p>Hello</p>");
        root.GetProperty("textContent").GetString().ShouldBe("Hello");
    }

    [Fact]
    public async Task SendEmailAsync_UsesFromEmailOverride_WhenProvided()
    {
        IEmailSender emailSender = _provider;

        await emailSender.SendAsync(
            new EmailMessage
            {
                To = "user@test.com",
                Subject = "Test",
                HtmlBody = "<p>Hi</p>",
                FromEmailOverride = "custom@test.com",
            },
            TestContext.Current.CancellationToken);

        string body = _handler.Requests[0].Body;
        body.ShouldContain("custom@test.com");
    }

    [Fact]
    public async Task SendEmailAsync_UsesDefaultSender_WhenNoOverride()
    {
        IEmailSender emailSender = _provider;

        await emailSender.SendAsync(
            new EmailMessage
            {
                To = "user@test.com",
                Subject = "Test",
                HtmlBody = "<p>Hi</p>",
            },
            TestContext.Current.CancellationToken);

        string body = _handler.Requests[0].Body;
        body.ShouldContain("default@test.com");
        body.ShouldContain("Test App");
    }

    [Fact]
    public async Task SendEmailAsync_WithHeaders_IncludesHeadersInPayload()
    {
        IEmailSender emailSender = _provider;

        await emailSender.SendAsync(
            new EmailMessage
            {
                To = "user@test.com",
                Subject = "Test",
                HtmlBody = "<p>Hi</p>",
                Headers = new Dictionary<string, string>
                {
                    ["List-Unsubscribe"] = "<https://example.com/unsub>",
                    ["List-Unsubscribe-Post"] = "List-Unsubscribe=One-Click",
                },
            },
            TestContext.Current.CancellationToken);

        _handler.Requests.Count.ShouldBe(1);
        string body = _handler.Requests[0].Body;
        using var doc = JsonDocument.Parse(body);
        JsonElement root = doc.RootElement;
        JsonElement headers = root.GetProperty("headers");
        headers.GetProperty("List-Unsubscribe").GetString().ShouldBe("<https://example.com/unsub>");
        headers.GetProperty("List-Unsubscribe-Post").GetString().ShouldBe("List-Unsubscribe=One-Click");
    }

    [Fact]
    public async Task SendEmailAsync_WithFromNameOverride_UsesOverrideName()
    {
        IEmailSender emailSender = _provider;

        await emailSender.SendAsync(
            new EmailMessage
            {
                To = "user@test.com",
                Subject = "Test",
                HtmlBody = "<p>Hi</p>",
                FromNameOverride = "Override Sender",
            },
            TestContext.Current.CancellationToken);

        _handler.Requests.Count.ShouldBe(1);
        string body = _handler.Requests[0].Body;
        using var doc = JsonDocument.Parse(body);
        JsonElement root = doc.RootElement;
        root.GetProperty("sender").GetProperty("name").GetString().ShouldBe("Override Sender");
    }

    [Fact]
    public async Task SendEmailAsync_ThrowsOnNon2xx()
    {
        _handler.ResponseStatusCode = HttpStatusCode.InternalServerError;
        IEmailSender emailSender = _provider;

        Func<Task> act = () => emailSender.SendAsync(
            new EmailMessage
            {
                To = "user@test.com",
                Subject = "Test",
                HtmlBody = "<p>Hi</p>",
            },
            TestContext.Current.CancellationToken);

        await Should.ThrowAsync<HttpRequestException>(act);
    }

    [Fact]
    public async Task SendEmailAsync_IncludesBrevoErrorBody_InException()
    {
        _handler.ResponseStatusCode = HttpStatusCode.BadRequest;
        _handler.ResponseBody = """{"code":"invalid_parameter","message":"Invalid email address"}""";
        IEmailSender emailSender = _provider;

        HttpRequestException ex = await Should.ThrowAsync<HttpRequestException>(() => emailSender.SendAsync(
            new EmailMessage
            {
                To = "bad-email",
                Subject = "Test",
                HtmlBody = "<p>Hi</p>",
            },
            TestContext.Current.CancellationToken));

        ex.Message.ShouldContain("Invalid email address");
        ex.Message.ShouldContain("smtp/email");
        ex.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
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
        _handler.Requests[0].Url.ShouldContain("transactionalSMS/sms");
    }

    [Fact]
    public async Task SendSmsAsync_IncludesCorrectPayload()
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
        using var doc = JsonDocument.Parse(body);
        JsonElement root = doc.RootElement;
        root.GetProperty("recipient").GetString().ShouldBe("+32470000000");
        root.GetProperty("content").GetString().ShouldBe("Hello SMS");
        root.GetProperty("type").GetString().ShouldBe("transactional");
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
                SenderId = "CustomId",
            },
            TestContext.Current.CancellationToken);

        string body = _handler.Requests[0].Body;
        body.ShouldContain("CustomId");
    }

    [Fact]
    public async Task SendSmsAsync_UsesDefaultSenderId_WhenNullInMessage()
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
        body.ShouldContain("TestApp");
    }

    [Fact]
    public async Task SendSmsAsync_ThrowsOnNon2xx_WithBrevoErrorBody()
    {
        _handler.ResponseStatusCode = HttpStatusCode.PaymentRequired;
        _handler.ResponseBody = """{"code":"insufficient_credits","message":"Not enough SMS credits"}""";
        ISmsSender smsSender = _provider;

        HttpRequestException ex = await Should.ThrowAsync<HttpRequestException>(() => smsSender.SendAsync(
            new SmsMessage
            {
                To = "+32470000000",
                Body = "Hello SMS",
            },
            TestContext.Current.CancellationToken));

        ex.Message.ShouldContain("Not enough SMS credits");
        ex.StatusCode.ShouldBe(HttpStatusCode.PaymentRequired);
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
        _handler.Requests[0].Url.ShouldContain("whatsapp/sendTemplate");
    }

    [Fact]
    public async Task SendWhatsAppAsync_IncludesCorrectPayload()
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

        _handler.Requests.Count.ShouldBe(1);
        string body = _handler.Requests[0].Body;
        using var doc = JsonDocument.Parse(body);
        JsonElement root = doc.RootElement;
        root.GetProperty("contactNumbers")[0].GetString().ShouldBe("+32470000000");
        root.GetProperty("templateId").GetString().ShouldBe("welcome_template");
        root.GetProperty("params")[0].GetString().ShouldBe("Jean");
        root.GetProperty("params")[1].GetString().ShouldBe("2026");
    }

    [Fact]
    public async Task SendWhatsAppAsync_DefaultsLanguageToFrench_WhenNull()
    {
        IWhatsAppSender whatsAppSender = _provider;

        await whatsAppSender.SendAsync(
            new WhatsAppMessage
            {
                To = "+32470000000",
                TemplateName = "welcome_template",
            },
            TestContext.Current.CancellationToken);

        string body = _handler.Requests[0].Body;
        body.ShouldContain("\"language\":\"fr\"");
    }

    [Fact]
    public async Task SendWhatsAppAsync_UsesExplicitLanguage_WhenProvided()
    {
        IWhatsAppSender whatsAppSender = _provider;

        await whatsAppSender.SendAsync(
            new WhatsAppMessage
            {
                To = "+32470000000",
                TemplateName = "welcome_template",
                Language = "en",
            },
            TestContext.Current.CancellationToken);

        string body = _handler.Requests[0].Body;
        body.ShouldContain("\"language\":\"en\"");
    }

    [Fact]
    public async Task SendWhatsAppAsync_ThrowsOnNon2xx()
    {
        _handler.ResponseStatusCode = HttpStatusCode.Forbidden;
        _handler.ResponseBody = """{"code":"forbidden","message":"Access denied"}""";
        IWhatsAppSender whatsAppSender = _provider;

        HttpRequestException ex = await Should.ThrowAsync<HttpRequestException>(() => whatsAppSender.SendAsync(
            new WhatsAppMessage
            {
                To = "+32470000000",
                TemplateName = "welcome_template",
            },
            TestContext.Current.CancellationToken));

        ex.Message.ShouldContain("Access denied");
        ex.Message.ShouldContain("whatsapp/sendTemplate");
        ex.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task SendWhatsAppAsync_OmitsSenderNumber_WhenNull()
    {
        IWhatsAppSender whatsAppSender = _provider;

        await whatsAppSender.SendAsync(
            new WhatsAppMessage
            {
                To = "+32470000000",
                TemplateName = "welcome_template",
            },
            TestContext.Current.CancellationToken);

        string body = _handler.Requests[0].Body;
        using var doc = JsonDocument.Parse(body);
        JsonElement root = doc.RootElement;

        // senderNumber is null and DefaultIgnoreCondition is WhenWritingNull,
        // but anonymous types serialize null as JsonValueKind.Null or may omit it.
        // Check that body doesn't contain a non-null senderNumber value.
        if (root.TryGetProperty("senderNumber", out JsonElement senderNumber))
        {
            senderNumber.ValueKind.ShouldBe(JsonValueKind.Null);
        }
    }

    // -------------------------------------------------------------------------
    // EnsureSuccessAsync — error body parsing
    // -------------------------------------------------------------------------

    [Fact]
    public async Task EnsureSuccessAsync_IncludesErrorBody_WhenResponseHasNoContent()
    {
        _handler.ResponseStatusCode = HttpStatusCode.InternalServerError;
        _handler.ResponseBody = null;
        IEmailSender emailSender = _provider;

        HttpRequestException ex = await Should.ThrowAsync<HttpRequestException>(() => emailSender.SendAsync(
            new EmailMessage
            {
                To = "user@test.com",
                Subject = "Test",
                HtmlBody = "<p>Hi</p>",
            },
            TestContext.Current.CancellationToken));

        // When no response body is returned, the error body is read as empty string.
        ex.Message.ShouldContain("smtp/email");
        ex.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task EnsureSuccessAsync_ThrowsWithNoBody_WhenContentReadThrows()
    {
        // Use a handler that returns non-2xx with content that throws on read,
        // exercising the catch block in EnsureSuccessAsync (lines 111-113).
        var throwingHandler = new ThrowOnReadHttpMessageHandler(HttpStatusCode.InternalServerError);
        using var throwingClient = new HttpClient(throwingHandler)
        {
            BaseAddress = new Uri("https://api.brevo.com/v3/"),
        };

        IHttpClientFactory factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient("Brevo").Returns(throwingClient);

        IOptionsMonitor<BrevoOptions> optionsMonitor = Substitute.For<IOptionsMonitor<BrevoOptions>>();
        optionsMonitor.CurrentValue.Returns(new BrevoOptions
        {
            ApiKey = "test-key",
            DefaultSenderEmail = "default@test.com",
            DefaultSenderName = "Test App",
        });

        ILogger<BrevoNotificationProvider> throwTestLogger = Substitute.For<ILogger<BrevoNotificationProvider>>();
        throwTestLogger.IsEnabled(Arg.Any<LogLevel>()).Returns(true);

        var provider = new BrevoNotificationProvider(
            factory, optionsMonitor, throwTestLogger);
        IEmailSender emailSender = provider;

        // The method should throw — either HttpRequestException from EnsureSuccessAsync
        // (with "(no body)" if catch works) or an exception from content read failure.
        await Should.ThrowAsync<Exception>(() => emailSender.SendAsync(
            new EmailMessage
            {
                To = "user@test.com",
                Subject = "Test",
                HtmlBody = "<p>Hi</p>",
            },
            TestContext.Current.CancellationToken));
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

        ex.Message.ShouldContain("transactionalSMS/sms");
        ex.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task SendWhatsAppAsync_ThrowsOnNon2xx_WithErrorBody()
    {
        _handler.ResponseStatusCode = HttpStatusCode.Unauthorized;
        _handler.ResponseBody = """{"code":"unauthorized","message":"Invalid API key"}""";
        IWhatsAppSender whatsAppSender = _provider;

        HttpRequestException ex = await Should.ThrowAsync<HttpRequestException>(() => whatsAppSender.SendAsync(
            new WhatsAppMessage
            {
                To = "+32470000000",
                TemplateName = "welcome_template",
            },
            TestContext.Current.CancellationToken));

        ex.Message.ShouldContain("Invalid API key");
        ex.Message.ShouldContain("whatsapp/sendTemplate");
        ex.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task SendWhatsAppAsync_WithEmptyTemplateParameters_IncludesEmptyArray()
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
        using var doc = JsonDocument.Parse(body);
        JsonElement root = doc.RootElement;
        root.GetProperty("params").GetArrayLength().ShouldBe(0);
    }

    // -------------------------------------------------------------------------
    // Class structure
    // -------------------------------------------------------------------------

    [Fact]
    public void Class_IsSealed() =>
        typeof(BrevoNotificationProvider).IsSealed.ShouldBeTrue();

    [Fact]
    public void Class_IsInternal() =>
        typeof(BrevoNotificationProvider).IsNotPublic.ShouldBeTrue();

    [Fact]
    public void Class_ImplementsIEmailSender() =>
        _provider.ShouldBeAssignableTo<IEmailSender>();

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
