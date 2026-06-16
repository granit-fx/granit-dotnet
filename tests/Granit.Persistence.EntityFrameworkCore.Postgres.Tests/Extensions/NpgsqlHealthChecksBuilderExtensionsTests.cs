using Granit.Persistence.EntityFrameworkCore.Postgres.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Shouldly;
using Xunit;

namespace Granit.Persistence.EntityFrameworkCore.Postgres.Tests.Extensions;

public sealed class NpgsqlHealthChecksBuilderExtensionsTests
{
    // Malformed connection string: the NpgsqlConnection constructor rejects the unknown
    // keyword synchronously, so the check's catch block runs without ever touching a real
    // server — deterministic and fast, no PostgreSQL required.
    private const string BadConnectionString = "NotAValidKeyword=1";

    [Fact]
    public async Task AddGranitPostgresHealthCheck_registers_default_named_check_reporting_failure()
    {
        ServiceCollection services = new();
        services.AddLogging();
        services.AddHealthChecks().AddGranitPostgresHealthCheck(BadConnectionString);

        await using ServiceProvider provider = services.BuildServiceProvider();
        HealthCheckService service = provider.GetRequiredService<HealthCheckService>();

        HealthReport report = await service.CheckHealthAsync(TestContext.Current.CancellationToken);

        report.Entries.ShouldContainKey("postgres");
        report.Entries["postgres"].Status.ShouldBe(HealthStatus.Unhealthy);
    }

    [Fact]
    public async Task AddGranitPostgresHealthCheck_honours_custom_name_and_tags()
    {
        ServiceCollection services = new();
        services.AddLogging();
        services.AddHealthChecks().AddGranitPostgresHealthCheck(
            BadConnectionString, name: "primary-db", tags: ["custom"]);

        await using ServiceProvider provider = services.BuildServiceProvider();
        HealthCheckService service = provider.GetRequiredService<HealthCheckService>();

        HealthReport report = await service.CheckHealthAsync(TestContext.Current.CancellationToken);

        report.Entries.ShouldContainKey("primary-db");
        report.Entries["primary-db"].Tags.ShouldContain("custom");
    }
}
