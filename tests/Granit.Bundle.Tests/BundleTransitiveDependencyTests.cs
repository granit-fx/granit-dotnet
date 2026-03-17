using Granit.Caching;
using Granit.Core.Modularity;
using Granit.Diagnostics;
using Granit.DocumentGeneration;
using Granit.DocumentGeneration.Excel;
using Granit.DocumentGeneration.Pdf;
using Granit.Features;
using Granit.Features.EntityFrameworkCore;
using Granit.Guids;
using Granit.Http.ApiDocumentation;
using Granit.Http.ApiVersioning;
using Granit.Http.Cors;
using Granit.Http.ExceptionHandling;
using Granit.Http.Idempotency;
using Granit.Localization;
using Granit.MultiTenancy;
using Granit.Notifications;
using Granit.Notifications.Email;
using Granit.Notifications.EntityFrameworkCore;
using Granit.Notifications.SignalR;
using Granit.Observability;
using Granit.Persistence;
using Granit.RateLimiting;
using Granit.Security;
using Granit.Templating;
using Granit.Templating.EntityFrameworkCore;
using Granit.Templating.Scriban;
using Granit.Timing;
using Granit.Validation;
using Shouldly;
using Xunit;

namespace Granit.Bundle.Tests;

/// <summary>
/// Verifies that meta-packages expose their transitive dependencies correctly.
/// If a type is not accessible, the project reference chain is broken.
/// </summary>
public sealed class BundleTransitiveDependencyTests
{
    [Fact]
    public void Essentials_ExposesAllTransitiveDependencies()
    {
        typeof(GranitModule).Assembly.ShouldNotBeNull();                 // Core
        typeof(IClock).Assembly.ShouldNotBeNull();                       // Timing
        typeof(IGuidGenerator).Assembly.ShouldNotBeNull();               // Guids
        typeof(ICurrentUserService).Assembly.ShouldNotBeNull();          // Security
        typeof(GranitValidationModule).Assembly.ShouldNotBeNull();       // Validation
        typeof(GranitPersistenceModule).Assembly.ShouldNotBeNull();      // Persistence
        typeof(GranitObservabilityModule).Assembly.ShouldNotBeNull();    // Observability
        typeof(GranitExceptionHandlingModule).Assembly.ShouldNotBeNull(); // ExceptionHandling
        typeof(GranitDiagnosticsModule).Assembly.ShouldNotBeNull();      // Diagnostics
    }

    [Fact]
    public void Api_IncludesEssentialsAndApiModules()
    {
        typeof(GranitModule).Assembly.ShouldNotBeNull();
        typeof(IClock).Assembly.ShouldNotBeNull();

        typeof(GranitHttpApiVersioningModule).Assembly.ShouldNotBeNull();
        typeof(GranitHttpApiDocumentationModule).Assembly.ShouldNotBeNull();
        typeof(GranitHttpCorsModule).Assembly.ShouldNotBeNull();
        typeof(GranitIdempotencyModule).Assembly.ShouldNotBeNull();
        typeof(GranitLocalizationModule).Assembly.ShouldNotBeNull();
        typeof(GranitCachingModule).Assembly.ShouldNotBeNull();
    }

    [Fact]
    public void Notifications_ExposesAllNotificationModules()
    {
        typeof(GranitNotificationsModule).Assembly.ShouldNotBeNull();
        typeof(GranitNotificationsEntityFrameworkCoreModule).Assembly.ShouldNotBeNull();
        typeof(EmailMessage).Assembly.ShouldNotBeNull();
        typeof(NotificationHub).Assembly.ShouldNotBeNull();
    }

    [Fact]
    public void Documents_ExposesAllDocumentModules()
    {
        typeof(GranitTemplatingModule).Assembly.ShouldNotBeNull();
        typeof(GranitTemplatingScribanModule).Assembly.ShouldNotBeNull();
        typeof(GranitTemplatingEntityFrameworkCoreModule).Assembly.ShouldNotBeNull();
        typeof(GranitDocumentGenerationModule).Assembly.ShouldNotBeNull();
        typeof(GranitDocumentGenerationPdfModule).Assembly.ShouldNotBeNull();
        typeof(GranitDocumentGenerationExcelModule).Assembly.ShouldNotBeNull();
    }

    [Fact]
    public void SaaS_ExposesAllSaaSModules()
    {
        typeof(GranitMultiTenancyModule).Assembly.ShouldNotBeNull();
        typeof(GranitFeaturesModule).Assembly.ShouldNotBeNull();
        typeof(GranitFeaturesEntityFrameworkCoreModule).Assembly.ShouldNotBeNull();
        typeof(GranitRateLimitingModule).Assembly.ShouldNotBeNull();
    }
}
