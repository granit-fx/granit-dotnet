using System.Globalization;
using Granit.Settings.Endpoints.Internal;
using Granit.Settings.Endpoints.Middleware;
using Granit.Settings.Services;
using Granit.Timing;
using Granit.Users;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Settings.Endpoints.Tests;

public sealed class SettingsCultureMiddlewareTests
{
    private readonly ISettingProvider _settingProvider = Substitute.For<ISettingProvider>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly ICurrentTimezoneProvider _timezoneProvider = Substitute.For<ICurrentTimezoneProvider>();
    private readonly ICurrentFirstDayOfWeekProvider _firstDayOfWeekProvider = Substitute.For<ICurrentFirstDayOfWeekProvider>();

    [Fact]
    public async Task Authenticated_User_With_Locale_Sets_CurrentUICulture()
    {
        _currentUserService.IsAuthenticated.Returns(true);
        _currentUserService.UserId.Returns("user-1");
        _settingProvider.GetOrNullAsync(WellKnownSettingNames.PreferredCulture, Arg.Any<CancellationToken>())
            .Returns("nl");
        _settingProvider.GetOrNullAsync(WellKnownSettingNames.PreferredTimezone, Arg.Any<CancellationToken>())
            .Returns((string?)null);

        CultureInfo? capturedCulture = null;

        SettingsCultureMiddleware middleware = new(_ =>
        {
            capturedCulture = CultureInfo.CurrentUICulture;
            return Task.CompletedTask;
        });

        HttpContext httpContext = CreateHttpContext();

        await middleware.InvokeAsync(httpContext);

        capturedCulture.ShouldNotBeNull();
        capturedCulture.Name.ShouldBe("nl");
    }

    [Fact]
    public async Task Authenticated_User_With_Timezone_Sets_TimezoneProvider()
    {
        _currentUserService.IsAuthenticated.Returns(true);
        _currentUserService.UserId.Returns("user-1");
        _settingProvider.GetOrNullAsync(WellKnownSettingNames.PreferredCulture, Arg.Any<CancellationToken>())
            .Returns((string?)null);
        _settingProvider.GetOrNullAsync(WellKnownSettingNames.PreferredTimezone, Arg.Any<CancellationToken>())
            .Returns("Europe/Brussels");

        SettingsCultureMiddleware middleware = new(_ => Task.CompletedTask);

        HttpContext httpContext = CreateHttpContext();

        await middleware.InvokeAsync(httpContext);

        _timezoneProvider.Received(1).Timezone = "Europe/Brussels";
    }

    [Fact]
    public async Task Anonymous_User_Is_NoOp()
    {
        _currentUserService.IsAuthenticated.Returns(false);
        _currentUserService.UserId.Returns((string?)null);

        CultureInfo originalCulture = CultureInfo.CurrentUICulture;
        CultureInfo? capturedCulture = null;

        SettingsCultureMiddleware middleware = new(_ =>
        {
            capturedCulture = CultureInfo.CurrentUICulture;
            return Task.CompletedTask;
        });

        HttpContext httpContext = CreateHttpContext();

        await middleware.InvokeAsync(httpContext);

        capturedCulture.ShouldNotBeNull();
        capturedCulture.ShouldBe(originalCulture);

        await _settingProvider.DidNotReceive()
            .GetOrNullAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Invalid_Stored_Locale_Is_Ignored()
    {
        _currentUserService.IsAuthenticated.Returns(true);
        _currentUserService.UserId.Returns("user-1");
        _settingProvider.GetOrNullAsync(WellKnownSettingNames.PreferredCulture, Arg.Any<CancellationToken>())
            .Returns("invalid-culture-xxx");
        _settingProvider.GetOrNullAsync(WellKnownSettingNames.PreferredTimezone, Arg.Any<CancellationToken>())
            .Returns((string?)null);

        CultureInfo originalCulture = CultureInfo.CurrentUICulture;
        CultureInfo? capturedCulture = null;

        SettingsCultureMiddleware middleware = new(_ =>
        {
            capturedCulture = CultureInfo.CurrentUICulture;
            return Task.CompletedTask;
        });

        HttpContext httpContext = CreateHttpContext();

        await middleware.InvokeAsync(httpContext);

        capturedCulture.ShouldNotBeNull();
        capturedCulture.ShouldBe(originalCulture, "culture invalide ignorée silencieusement");
    }

    [Fact]
    public async Task Authenticated_User_With_Both_Locale_And_Timezone_Sets_Both()
    {
        _currentUserService.IsAuthenticated.Returns(true);
        _currentUserService.UserId.Returns("user-1");
        _settingProvider.GetOrNullAsync(WellKnownSettingNames.PreferredCulture, Arg.Any<CancellationToken>())
            .Returns("de");
        _settingProvider.GetOrNullAsync(WellKnownSettingNames.PreferredTimezone, Arg.Any<CancellationToken>())
            .Returns("Europe/Berlin");

        CultureInfo? capturedCulture = null;

        SettingsCultureMiddleware middleware = new(_ =>
        {
            capturedCulture = CultureInfo.CurrentUICulture;
            return Task.CompletedTask;
        });

        DefaultHttpContext httpContext = CreateHttpContext();

        await middleware.InvokeAsync(httpContext);

        capturedCulture.ShouldNotBeNull();
        capturedCulture.Name.ShouldBe("de");
        _timezoneProvider.Received(1).Timezone = "Europe/Berlin";
    }

    [Fact]
    public async Task Timezone_Without_ICurrentTimezoneProvider_Is_Skipped()
    {
        _currentUserService.IsAuthenticated.Returns(true);
        _currentUserService.UserId.Returns("user-1");
        _settingProvider.GetOrNullAsync(WellKnownSettingNames.PreferredCulture, Arg.Any<CancellationToken>())
            .Returns((string?)null);
        _settingProvider.GetOrNullAsync(WellKnownSettingNames.PreferredTimezone, Arg.Any<CancellationToken>())
            .Returns("Europe/Brussels");

        SettingsCultureMiddleware middleware = new(_ => Task.CompletedTask);

        // HttpContext without ICurrentTimezoneProvider registered.
        ServiceCollection services = new();
        services.AddSingleton(_settingProvider);
        services.AddSingleton(_currentUserService);
        DefaultHttpContext httpContext = new() { RequestServices = services.BuildServiceProvider() };

        // Should not throw — graceful skip.
        await Should.NotThrowAsync(() => middleware.InvokeAsync(httpContext));
    }

    [Fact]
    public async Task Locale_Also_Sets_CurrentCulture()
    {
        _currentUserService.IsAuthenticated.Returns(true);
        _currentUserService.UserId.Returns("user-1");
        _settingProvider.GetOrNullAsync(WellKnownSettingNames.PreferredCulture, Arg.Any<CancellationToken>())
            .Returns("es");
        _settingProvider.GetOrNullAsync(WellKnownSettingNames.PreferredTimezone, Arg.Any<CancellationToken>())
            .Returns((string?)null);

        CultureInfo? capturedCulture = null;

        SettingsCultureMiddleware middleware = new(_ =>
        {
            capturedCulture = CultureInfo.CurrentCulture;
            return Task.CompletedTask;
        });

        DefaultHttpContext httpContext = CreateHttpContext();

        await middleware.InvokeAsync(httpContext);

        capturedCulture.ShouldNotBeNull();
        capturedCulture.Name.ShouldBe("es", "CurrentCulture (pas seulement CurrentUICulture) doit être défini");
    }

    [Fact]
    public async Task Authenticated_User_With_FirstDayOfWeek_Sets_Provider_CaseInsensitively()
    {
        _currentUserService.IsAuthenticated.Returns(true);
        _currentUserService.UserId.Returns("user-1");
        _settingProvider.GetOrNullAsync(WellKnownSettingNames.PreferredCulture, Arg.Any<CancellationToken>())
            .Returns((string?)null);
        _settingProvider.GetOrNullAsync(WellKnownSettingNames.PreferredTimezone, Arg.Any<CancellationToken>())
            .Returns((string?)null);
        _settingProvider.GetOrNullAsync(WellKnownSettingNames.PreferredFirstDayOfWeek, Arg.Any<CancellationToken>())
            .Returns("monday");

        SettingsCultureMiddleware middleware = new(_ => Task.CompletedTask);

        await middleware.InvokeAsync(CreateHttpContext());

        _firstDayOfWeekProvider.Received(1).FirstDayOfWeek = DayOfWeek.Monday;
    }

    [Fact]
    public async Task Authenticated_User_With_Invalid_FirstDayOfWeek_Is_Ignored()
    {
        _currentUserService.IsAuthenticated.Returns(true);
        _currentUserService.UserId.Returns("user-1");
        _settingProvider.GetOrNullAsync(WellKnownSettingNames.PreferredCulture, Arg.Any<CancellationToken>())
            .Returns((string?)null);
        _settingProvider.GetOrNullAsync(WellKnownSettingNames.PreferredTimezone, Arg.Any<CancellationToken>())
            .Returns((string?)null);
        _settingProvider.GetOrNullAsync(WellKnownSettingNames.PreferredFirstDayOfWeek, Arg.Any<CancellationToken>())
            .Returns("Funday");

        SettingsCultureMiddleware middleware = new(_ => Task.CompletedTask);

        await middleware.InvokeAsync(CreateHttpContext());

        _firstDayOfWeekProvider.DidNotReceiveWithAnyArgs().FirstDayOfWeek = default;
    }

    private DefaultHttpContext CreateHttpContext()
    {
        ServiceCollection services = new();
        services.AddSingleton(_settingProvider);
        services.AddSingleton(_currentUserService);
        services.AddSingleton(_timezoneProvider);
        services.AddSingleton(_firstDayOfWeekProvider);

        DefaultHttpContext httpContext = new()
        {
            RequestServices = services.BuildServiceProvider(),
        };

        return httpContext;
    }
}
