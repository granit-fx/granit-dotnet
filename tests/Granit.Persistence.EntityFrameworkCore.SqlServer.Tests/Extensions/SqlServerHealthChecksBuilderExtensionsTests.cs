using Granit.Persistence.EntityFrameworkCore.SqlServer.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Shouldly;
using Xunit;

namespace Granit.Persistence.EntityFrameworkCore.SqlServer.Tests.Extensions;

public sealed class SqlServerHealthChecksBuilderExtensionsTests
{
    // Malformed connection string: the SqlConnection constructor rejects the unknown
    // keyword synchronously, so the check's catch block runs without ever touching a
    // real server — deterministic and fast, no SQL Server required.
    private const string BadConnectionString = "NotAValidKeyword=1";

    [Fact]
    public async Task AddGranitSqlServerHealthCheck_registers_default_named_check_reporting_failure()
    {
        ServiceCollection services = new();
        services.AddLogging();
        services.AddHealthChecks().AddGranitSqlServerHealthCheck(BadConnectionString);

        await using ServiceProvider provider = services.BuildServiceProvider();
        HealthCheckService service = provider.GetRequiredService<HealthCheckService>();

        HealthReport report = await service.CheckHealthAsync(TestContext.Current.CancellationToken);

        report.Entries.ShouldContainKey("sqlserver");
        report.Entries["sqlserver"].Status.ShouldBe(HealthStatus.Unhealthy);
    }

    [Fact]
    public async Task AddGranitSqlServerHealthCheck_honours_custom_name_and_tags()
    {
        ServiceCollection services = new();
        services.AddLogging();
        services.AddHealthChecks().AddGranitSqlServerHealthCheck(
            BadConnectionString, name: "primary-db", tags: ["custom"]);

        await using ServiceProvider provider = services.BuildServiceProvider();
        HealthCheckService service = provider.GetRequiredService<HealthCheckService>();

        HealthReport report = await service.CheckHealthAsync(TestContext.Current.CancellationToken);

        report.Entries.ShouldContainKey("primary-db");
        report.Entries["primary-db"].Tags.ShouldContain("custom");
    }
}
