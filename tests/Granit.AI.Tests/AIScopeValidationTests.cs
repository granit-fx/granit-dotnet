using Granit.AI.Extensions;
using Granit.AI.Tenancy;
using Granit.Authorization;
using Granit.MultiTenancy;
using Granit.Settings.Services;
using Granit.Timing.Extensions;
using Granit.Users;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using NSubstitute;
using Shouldly;

namespace Granit.AI.Tests;

public sealed class AIScopeValidationTests
{
    [Fact]
    public void AddGranitAI_should_pass_scope_validation_when_setting_manager_is_singleton()
    {
        // Regression: AISettingsCredentialsGuard decorates ISettingManager and previously
        // captured the scoped IPermissionChecker directly in the decorator factory. When
        // the underlying ISettingManager was registered as a singleton (e.g. through an
        // ImplementationInstance), the guard was forced to singleton and the captive
        // IPermissionChecker either failed ValidateScopes at startup or silently leaked
        // the first request's scope. The fix routes the checker through a singleton-safe
        // wrapper (ScopedPermissionChecker) backed by IServiceScopeFactory whenever the
        // guard's lifetime is Singleton.
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(null);
        builder.Configuration.AddInMemoryCollection();

        builder.Services.AddLogging();
        builder.Services.AddMetrics();
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddGranitTiming();
        builder.Services.AddSingleton<ICurrentTenant>(new NullTenantContext());
        builder.Services.TryAddScoped(_ => Substitute.For<ICurrentUserService>());

        // The scoped registration that previously broke ValidateScopes once the guard
        // wrapped a singleton ISettingManager.
        builder.Services.TryAddScoped(_ => Substitute.For<IPermissionChecker>());

        // Singleton ISettingManager — the path that hardcoded AddSingleton on the
        // decorator and triggered the captive dependency.
        ISettingManager innerSettings = Substitute.For<ISettingManager>();
        builder.Services.AddSingleton(innerSettings);

        builder.AddGranitAI();

        // ValidateScopes catches the captive IPermissionChecker on the first resolve;
        // ValidateOnBuild is intentionally off so the audit stays focused on the guard
        // (otherwise it would fail on every transitive AI dep we didn't bother to stub).
        Should.NotThrow(() =>
        {
            using ServiceProvider provider = builder.Services.BuildServiceProvider(
                new ServiceProviderOptions { ValidateScopes = true });

            using IServiceScope scope = provider.CreateScope();
            ISettingManager resolved = scope.ServiceProvider.GetRequiredService<ISettingManager>();
            resolved.ShouldBeOfType<AISettingsCredentialsGuard>();
        });
    }
}
