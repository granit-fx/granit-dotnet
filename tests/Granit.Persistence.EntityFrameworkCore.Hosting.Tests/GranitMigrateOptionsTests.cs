using Granit.Persistence.EntityFrameworkCore.Hosting.Options;
using Shouldly;
using Xunit;

namespace Granit.Persistence.EntityFrameworkCore.Hosting.Tests;

public class GranitMigrateOptionsTests
{
    [Fact]
    public void Defaults_should_be_production_safe()
    {
        var options = new GranitMigrateOptions();

        options.CliFlag.ShouldBe("--migrate");
        options.SeedAfterMigration.ShouldBeTrue();
        options.Timeout.ShouldBe(TimeSpan.FromMinutes(5));
        options.SeedOnStartup.ShouldBeFalse();
        options.MaxRetries.ShouldBe(3);
        options.RetryDelay.ShouldBe(TimeSpan.FromSeconds(5));
    }
}
