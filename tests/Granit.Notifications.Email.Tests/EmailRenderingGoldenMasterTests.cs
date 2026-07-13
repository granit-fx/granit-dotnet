// =============================================================================
// GOLDEN MASTER — full email rendering pipeline
// =============================================================================
// Freezes the end-to-end render of the reference host wiring (real Scriban
// engine, real embedded templates from Granit.Privacy.Notifications, real
// Layout.Email wrap, real <title> subject extraction, real AngleSharp
// plain-text conversion, real List-Unsubscribe headers) BEFORE the pipeline is
// extracted into INotificationContentRenderer (phase 4, #2963).
//
// The refactor is mergeable only if these tests pass unchanged. If a diff in
// the expected files is ever legitimate, it must be explained in the PR.
// Expected files live under GoldenMaster/ and are compared byte-for-byte
// (modulo trailing whitespace per line).
// =============================================================================

using System.Text.Json;
using Granit.Html.AngleSharp;
using Granit.Localization.Extensions;
using Granit.Notifications.Abstractions;
using Granit.Notifications.Email.Extensions;
using Granit.Notifications.Email.Internal;
using Granit.Notifications.Email.Options;
using Granit.Notifications.Extensions;
using Granit.Privacy.Notifications;
using Granit.Templating.Extensions;
using Granit.Templating.Mjml.Extensions;
using Granit.Templating.Scriban.Extensions;
using Granit.Timing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Notifications.Email.Tests;

public sealed class EmailRenderingGoldenMasterTests
{
    private static readonly DateTimeOffset FrozenNow = new(2026, 3, 16, 14, 0, 0, TimeSpan.Zero);

    private static async Task<EmailMessage> RenderAsync(
        string notificationTypeName, object data, string? preferredCulture)
    {
        ServiceCollection services = new();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Templating:App:BaseUrl"] = "https://app.golden.test",
        }).Build());

        IClock clock = Substitute.For<IClock>();
        clock.Now.Returns(FrozenNow);
        services.AddSingleton(clock);

        // Reference host wiring: Scriban engine + embedded templates of the privacy
        // satellite and of the Email package (Notifications.Default + Layout.Email).
        // The DB-backed template store is stubbed empty — resolution falls through to the
        // embedded resolver, like a host without Granit.Templating.EntityFrameworkCore.
        services.AddSingleton(Substitute.For<Granit.Templating.Store.IDocumentTemplateStoreReader>());
        services.AddGranitTemplatingWithScriban();
        services.AddGranitTemplatingWithMjml();
        services.AddLogging();
        services.AddGranitNotificationContentRenderer();

        // Real JSON localization so the layout's {{ t "NotificationsEmail:*" }} calls render
        // the production strings, not raw keys. The FusionCache-backed override store is
        // stubbed empty — JSON files are the source, like a host without DB overrides.
        services.AddSingleton(Substitute.For<Granit.Localization.ILocalizationOverrideStoreReader>());
        services.AddSingleton(Substitute.For<Granit.Localization.ILocalizationOverrideStoreWriter>());
        services.AddGranitLocalization();
        services.AddLocalizationResource<NotificationsEmailLocalizationResource>();
        services.AddEmbeddedTemplates(typeof(GranitPrivacyNotificationsModule).Assembly);
        services.AddEmbeddedTemplates(typeof(EmailNotificationsServiceCollectionExtensions).Assembly);
        services.AddTemplateLayout("privacy.*", "Layout.Email");
        // Stand-in for PrivacyContactGlobalContext (which needs Granit.Settings): empty
        // values, identical to a host with no privacy settings configured.
        services.AddTemplateGlobalContext<EmptyPrivacyGlobalContext>();
        services.AddTemplateLayout("Notifications.*", "Layout.Email");

        EmailMessage? captured = null;
        IEmailSender sender = Substitute.For<IEmailSender>();
        sender.SendAsync(Arg.Do<EmailMessage>(m => captured = m), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        services.AddKeyedSingleton("Golden", (_, _) => sender);

        await using ServiceProvider sp = services.BuildServiceProvider();

        IRecipientResolver recipientResolver = Substitute.For<IRecipientResolver>();
        recipientResolver.ResolveAsync("user-1", Arg.Any<CancellationToken>())
            .Returns(new RecipientInfo
            {
                UserId = "user-1",
                Email = "golden@test.example",
                DisplayName = "Golden Master",
                PreferredCulture = preferredCulture,
            });

        EmailNotificationChannel channel = new(
            sp,
            Microsoft.Extensions.Options.Options.Create(new EmailChannelOptions
            {
                Provider = "Golden",
                DefaultSenderEmail = "no-reply@golden.test",
                DefaultSenderName = "Golden",
                UnsubscribeUrl = "https://app.golden.test/unsubscribe",
            }),
            recipientResolver,
            sp.GetRequiredService<IConfiguration>(),
            new CurrentTimezoneProvider(),
            new AngleSharpHtmlToPlainTextConverter(),
            new XunitRecordingLogger(),
            sp.GetRequiredService<Granit.Notifications.Rendering.INotificationContentRenderer>());

        await channel.SendAsync(new NotificationDeliveryContext
        {
            DeliveryId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            NotificationId = Guid.Parse("22222222-2222-2222-2222-222222222222"),
            NotificationTypeName = notificationTypeName,
            RecipientUserId = "user-1",
            Severity = NotificationSeverity.Info,
            Data = JsonSerializer.SerializeToElement(data),
            OccurredAt = FrozenNow,
        }, TestContext.Current.CancellationToken);

        captured.ShouldNotBeNull();
        return captured;
    }

    private static readonly object ExportReadyData = new
    {
        request_id = "33333333-3333-3333-3333-333333333333",
        shard_count = 3,
        requested_at = "2026-03-10T09:30:00+00:00",
        regulation = "GDPR",
    };

    [Fact]
    public async Task TypeTemplate_NeutralCulture_MatchesGoldenMaster()
    {
        EmailMessage email = await RenderAsync("privacy.export_ready", ExportReadyData, preferredCulture: null);

        email.Subject.ShouldBe("Your personal data export is ready");
        email.Headers.ShouldNotBeNull();
        email.Headers["List-Unsubscribe"].ShouldBe("<https://app.golden.test/unsubscribe>");
        email.Headers["List-Unsubscribe-Post"].ShouldBe("List-Unsubscribe=One-Click");
        AssertMatchesGolden("export_ready.en.html", email.HtmlBody);
        AssertMatchesGolden("export_ready.en.txt", email.PlainTextBody!);
    }

    [Fact]
    public async Task TypeTemplate_FrenchRecipient_MatchesGoldenMaster()
    {
        EmailMessage email = await RenderAsync("privacy.export_ready", ExportReadyData, preferredCulture: "fr");

        AssertMatchesGolden("export_ready.fr.html", email.HtmlBody);
    }

    [Fact]
    public async Task UnknownType_FallsBackToDefaultTemplate_MatchesGoldenMaster()
    {
        EmailMessage email = await RenderAsync(
            "unit.unknown_type", new { some_field = "some value" }, preferredCulture: null);

        AssertMatchesGolden("fallback.en.html", email.HtmlBody);
    }

    private static void AssertMatchesGolden(string expectedFileName, string actual)
    {
        string dir = Path.Join(
            Path.GetDirectoryName(typeof(EmailRenderingGoldenMasterTests).Assembly.Location),
            "GoldenMaster");
        string expectedPath = Path.Join(dir, expectedFileName);

        string normalizedActual = Normalize(actual);

        if (!File.Exists(expectedPath))
        {
            // First-run capture: write the actual so it can be reviewed and committed as the
            // golden master, then fail loudly — an expected file must never be missing in CI.
            Directory.CreateDirectory(dir);
            File.WriteAllText(expectedPath + ".actual", normalizedActual);
            throw new ShouldAssertException(
                $"Golden master '{expectedFileName}' is missing. Review '{expectedPath}.actual' and commit it as '{expectedFileName}'.");
        }

        Normalize(File.ReadAllText(expectedPath)).ShouldBe(normalizedActual);
    }

    private sealed class EmptyPrivacyGlobalContext : Granit.Templating.GlobalContext.ITemplateGlobalContext
    {
        public string ContextName => "privacy";
        public object Resolve() => new { dpo_email = "", controller_name = "" };
    }

    private sealed class XunitRecordingLogger : Microsoft.Extensions.Logging.ILogger<EmailNotificationChannel>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => true;
        public void Log<TState>(Microsoft.Extensions.Logging.LogLevel logLevel, Microsoft.Extensions.Logging.EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            TestContext.Current.TestOutputHelper?.WriteLine($"[{logLevel}] {formatter(state, exception)} {exception}");
    }

    private static string Normalize(string text) =>
        string.Join('\n', text.Replace("\r\n", "\n").Split('\n').Select(l => l.TrimEnd()));
}
