using Granit.Caching;
using Granit.Diagnostics;
using Granit.Guids;
using Granit.Http.ApiDocumentation;
using Granit.Http.ApiVersioning;
using Granit.Http.ExceptionHandling;
using Granit.Http.Hosting;
using Granit.Http.Idempotency;
using Granit.Localization;
using Granit.Modularity;
using Granit.Notifications;
using Granit.Notifications.Email;
using Granit.Notifications.EntityFrameworkCore;
using Granit.Notifications.SignalR;
using Granit.Observability;
using Granit.Persistence.EntityFrameworkCore;
using Granit.Timing;
using Granit.Users;
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
        typeof(GranitPersistenceEntityFrameworkCoreModule).Assembly.ShouldNotBeNull();      // Persistence
        typeof(GranitObservabilityModule).Assembly.ShouldNotBeNull();    // Observability
        typeof(GranitHttpExceptionHandlingModule).Assembly.ShouldNotBeNull(); // ExceptionHandling
        typeof(GranitDiagnosticsModule).Assembly.ShouldNotBeNull();      // Diagnostics
    }

    [Fact]
    public void Api_IncludesEssentialsAndApiModules()
    {
        typeof(GranitModule).Assembly.ShouldNotBeNull();
        typeof(IClock).Assembly.ShouldNotBeNull();

        typeof(GranitHttpApiVersioningModule).Assembly.ShouldNotBeNull();
        typeof(GranitHttpApiDocumentationModule).Assembly.ShouldNotBeNull();
        typeof(GranitHttpHostingModule).Assembly.ShouldNotBeNull();
        typeof(GranitHttpIdempotencyModule).Assembly.ShouldNotBeNull();
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

}
