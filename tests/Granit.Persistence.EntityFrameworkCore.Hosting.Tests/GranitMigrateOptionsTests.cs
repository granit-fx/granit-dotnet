using Granit.Persistence.EntityFrameworkCore.Hosting.Options;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Shouldly;
using Xunit;

namespace Granit.Persistence.EntityFrameworkCore.Hosting.Tests;

public sealed class GranitMigrateOptionsTests
{
    [Fact]
    public void Defaults_should_be_production_safe()
    {
        var options = new GranitMigrateOptions();

        options.CliFlag.ShouldBe("--migrate");
        options.ConnectionStringName.ShouldBe("DefaultConnection");
        options.RequireDistributedLock.ShouldBeNull();
        options.SeedAfterMigration.ShouldBeTrue();
        options.Timeout.ShouldBe(TimeSpan.FromMinutes(5));
        options.SeedOnStartup.ShouldBeFalse();
        options.MaxRetries.ShouldBe(3);
        options.RetryDelay.ShouldBe(TimeSpan.FromSeconds(5));
    }

    [Theory]
    // Explicit setting always wins, whatever the environment.
    [InlineData(true, "Development", true)]
    [InlineData(false, "Production", false)]
    // Unset: required outside Development…
    [InlineData(null, "Production", true)]
    [InlineData(null, "Staging", true)]
    [InlineData(null, "Development", false)]
    public void IsDistributedLockRequired_ResolvesExplicitThenEnvironment(
        bool? configured, string environmentName, bool expected)
    {
        var options = new GranitMigrateOptions { RequireDistributedLock = configured };

        options.IsDistributedLockRequired(new FakeEnvironment(environmentName))
            .ShouldBe(expected);
    }

    [Fact]
    // No IHostEnvironment available (bare DI, tools) → production assumption.
    public void IsDistributedLockRequired_UnknownEnvironment_FailsClosed() =>
        new GranitMigrateOptions().IsDistributedLockRequired(null).ShouldBeTrue();

    private sealed class FakeEnvironment(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "tests";
        public string ContentRootPath { get; set; } = ".";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    [Fact]
    // Contract with appsettings/Helm overlays — renaming silently orphans deployed config.
    public void SectionName_is_the_documented_configuration_section() =>
        GranitMigrateOptions.SectionName.ShouldBe("Persistence:Migrate");
}
