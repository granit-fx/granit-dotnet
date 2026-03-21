using Granit.Diagnostics.Dtos;
using Shouldly;
using Xunit;

namespace Granit.Diagnostics.Tests;

public sealed class MonitoringHealthResponseTests
{
    [Fact]
    public void MonitoringHealthResponse_StoresProperties_Correctly()
    {
        DateTimeOffset checkedAt = new(2026, 3, 20, 12, 0, 0, TimeSpan.Zero);
        List<ServiceHealthResponse> services =
        [
            new ServiceHealthResponse("db", "Db", "healthy", 5.2, null, [])
        ];

        MonitoringHealthResponse response = new(services, checkedAt);

        response.Services.ShouldBe(services);
        response.CheckedAt.ShouldBe(checkedAt);
    }

    [Fact]
    public void ServiceHealthResponse_StoresAllProperties()
    {
        ServiceHealthResponse service = new(
            Id: "blob-storage-s3",
            Name: "Blob Storage S3",
            Status: "healthy",
            ResponseTimeMs: 12.3,
            Description: "All good",
            Tags: ["readiness", "startup"]);

        service.Id.ShouldBe("blob-storage-s3");
        service.Name.ShouldBe("Blob Storage S3");
        service.Status.ShouldBe("healthy");
        service.ResponseTimeMs.ShouldBe(12.3);
        service.Description.ShouldBe("All good");
        service.Tags.ShouldBe(["readiness", "startup"]);
    }

    [Fact]
    public void ServiceHealthResponse_AllowsNullDescription()
    {
        ServiceHealthResponse service = new("db", "Db", "healthy", 5.0, null, []);

        service.Description.ShouldBeNull();
    }

    [Fact]
    public void ServiceHealthResponse_AllowsNullResponseTimeMs()
    {
        ServiceHealthResponse service = new("db", "Db", "healthy", null, null, []);

        service.ResponseTimeMs.ShouldBeNull();
    }

    [Fact]
    public void MonitoringHealthResponse_SupportsEmptyServicesList()
    {
        DateTimeOffset checkedAt = DateTimeOffset.UtcNow;
        MonitoringHealthResponse response = new([], checkedAt);

        response.Services.ShouldBeEmpty();
    }
}
