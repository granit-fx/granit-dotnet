using Granit.Notifications.Domain;
using Granit.Notifications.EntityFrameworkCore.Extensions;
using Granit.QueryEngine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;
using Xunit;

namespace Granit.Notifications.EntityFrameworkCore.Tests;

/// <summary>
/// Regression: the query engine + analytics runner resolve <see cref="IQueryableSource{T}"/>
/// for <c>UserNotificationQuery</c> / <c>NotificationPreferenceQuery</c>. Without the
/// registrations the grids throw "No service for type IQueryableSource&lt;T&gt;" at first request.
/// </summary>
public sealed class NotificationsEfCoreQueryableSourceRegistrationTests
{
    [Fact]
    public void AddGranitNotificationsEntityFrameworkCore_RegistersQueryableSource_ForUserNotification_AsScoped()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder([]);

        builder.AddGranitNotificationsEntityFrameworkCore(_ => { });

        builder.Services.ShouldContain(d =>
            d.ServiceType == typeof(IQueryableSource<UserNotification>) &&
            d.Lifetime == ServiceLifetime.Scoped);
    }

    [Fact]
    public void AddGranitNotificationsEntityFrameworkCore_RegistersQueryableSource_ForNotificationPreference_AsScoped()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder([]);

        builder.AddGranitNotificationsEntityFrameworkCore(_ => { });

        builder.Services.ShouldContain(d =>
            d.ServiceType == typeof(IQueryableSource<NotificationPreference>) &&
            d.Lifetime == ServiceLifetime.Scoped);
    }
}
