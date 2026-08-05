using Granit.Persistence.EntityFrameworkCore.Hosting.Options;
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
        options.SeedAfterMigration.ShouldBeTrue();
        options.Timeout.ShouldBe(TimeSpan.FromMinutes(5));
        options.SeedOnStartup.ShouldBeFalse();
        options.MaxRetries.ShouldBe(3);
        options.RetryDelay.ShouldBe(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void SectionName_is_the_documented_configuration_section()
    {
        // Contract with appsettings/Helm overlays — renaming silently orphans deployed config.
        GranitMigrateOptions.SectionName.ShouldBe("Persistence:Migrate");
    }
}
