using System.Text.Json;
using Granit.Diagnostics.ResponseWriters;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Shouldly;
using Xunit;

namespace Granit.Diagnostics.Tests;

public sealed class GranitHealthCheckWriterTests
{
    [Fact]
    public async Task WriteAsync_SetsContentType_ToApplicationJson()
    {
        DefaultHttpContext httpContext = new();
        httpContext.Response.Body = new MemoryStream();
        HealthReport report = BuildReport(HealthStatus.Healthy);

        await GranitHealthCheckWriter.WriteAsync(httpContext, report);

        httpContext.Response.ContentType.ShouldBe("application/json; charset=utf-8");
    }

    [Fact]
    public async Task WriteAsync_WritesStatus_AsString()
    {
        DefaultHttpContext httpContext = new();
        httpContext.Response.Body = new MemoryStream();
        HealthReport report = BuildReport(HealthStatus.Unhealthy);

        await GranitHealthCheckWriter.WriteAsync(httpContext, report);

        JsonDocument json = ParseResponse(httpContext);
        json.RootElement.GetProperty("status").GetString().ShouldBe("Unhealthy");
    }

    [Fact]
    public async Task WriteAsync_WritesChecks_WithNameAndStatus()
    {
        DefaultHttpContext httpContext = new();
        httpContext.Response.Body = new MemoryStream();

        Dictionary<string, HealthReportEntry> entries = new()
        {
            ["efcore"] = new HealthReportEntry(HealthStatus.Healthy, null, TimeSpan.FromMilliseconds(8), null, null, ["readiness"]),
            ["vault"] = new HealthReportEntry(HealthStatus.Degraded, "Vault standby", TimeSpan.FromMilliseconds(4), null, null, ["readiness"])
        };
        HealthReport report = new(entries, TimeSpan.FromMilliseconds(12));

        await GranitHealthCheckWriter.WriteAsync(httpContext, report);

        JsonDocument json = ParseResponse(httpContext);
        JsonElement checks = json.RootElement.GetProperty("checks");
        checks.GetArrayLength().ShouldBe(2);

        JsonElement efcore = checks.EnumerateArray().First(e => e.GetProperty("name").GetString() == "efcore");
        efcore.GetProperty("status").GetString().ShouldBe("Healthy");

        JsonElement vault = checks.EnumerateArray().First(e => e.GetProperty("name").GetString() == "vault");
        vault.GetProperty("status").GetString().ShouldBe("Degraded");
        vault.GetProperty("description").GetString().ShouldBe("Vault standby");
    }

    [Fact]
    public async Task WriteAsync_LivenessResponse_HasEmptyChecks()
    {
        DefaultHttpContext httpContext = new();
        httpContext.Response.Body = new MemoryStream();
        HealthReport report = new(new Dictionary<string, HealthReportEntry>(), TimeSpan.Zero);

        await GranitHealthCheckWriter.WriteAsync(httpContext, report);

        JsonDocument json = ParseResponse(httpContext);
        json.RootElement.GetProperty("checks").GetArrayLength().ShouldBe(0);
    }

    [Fact]
    public async Task WriteAsync_WriteDuration_RoundedToOneDecimal()
    {
        DefaultHttpContext httpContext = new();
        httpContext.Response.Body = new MemoryStream();

        Dictionary<string, HealthReportEntry> entries = new()
        {
            ["db"] = new HealthReportEntry(HealthStatus.Healthy, null, TimeSpan.FromMilliseconds(12.789), null, null, [])
        };
        HealthReport report = new(entries, TimeSpan.FromMilliseconds(12.789));

        await GranitHealthCheckWriter.WriteAsync(httpContext, report);

        JsonDocument json = ParseResponse(httpContext);
        json.RootElement.GetProperty("duration").GetDouble().ShouldBe(12.8);

        JsonElement check = json.RootElement.GetProperty("checks").EnumerateArray().First();
        check.GetProperty("duration").GetDouble().ShouldBe(12.8);
    }

    [Fact]
    public async Task WriteAsync_OmitsDescription_WhenNull()
    {
        DefaultHttpContext httpContext = new();
        httpContext.Response.Body = new MemoryStream();

        Dictionary<string, HealthReportEntry> entries = new()
        {
            ["db"] = new HealthReportEntry(HealthStatus.Healthy, null, TimeSpan.FromMilliseconds(5), null, null, [])
        };
        HealthReport report = new(entries, TimeSpan.FromMilliseconds(5));

        await GranitHealthCheckWriter.WriteAsync(httpContext, report);

        JsonDocument json = ParseResponse(httpContext);
        JsonElement check = json.RootElement.GetProperty("checks").EnumerateArray().First();
        check.TryGetProperty("description", out _).ShouldBeFalse("Null description should be omitted from JSON");
    }

    [Fact]
    public async Task WriteAsync_IncludesDescription_WhenPresent()
    {
        DefaultHttpContext httpContext = new();
        httpContext.Response.Body = new MemoryStream();

        Dictionary<string, HealthReportEntry> entries = new()
        {
            ["db"] = new HealthReportEntry(HealthStatus.Degraded, "High latency", TimeSpan.FromMilliseconds(5), null, null, [])
        };
        HealthReport report = new(entries, TimeSpan.FromMilliseconds(5));

        await GranitHealthCheckWriter.WriteAsync(httpContext, report);

        JsonDocument json = ParseResponse(httpContext);
        JsonElement check = json.RootElement.GetProperty("checks").EnumerateArray().First();
        check.GetProperty("description").GetString().ShouldBe("High latency");
    }

    [Fact]
    public async Task WriteAsync_WritesTags_AsArray()
    {
        DefaultHttpContext httpContext = new();
        httpContext.Response.Body = new MemoryStream();

        Dictionary<string, HealthReportEntry> entries = new()
        {
            ["db"] = new HealthReportEntry(HealthStatus.Healthy, null, TimeSpan.FromMilliseconds(5), null, null, ["readiness", "startup"])
        };
        HealthReport report = new(entries, TimeSpan.FromMilliseconds(5));

        await GranitHealthCheckWriter.WriteAsync(httpContext, report);

        JsonDocument json = ParseResponse(httpContext);
        JsonElement check = json.RootElement.GetProperty("checks").EnumerateArray().First();
        JsonElement tags = check.GetProperty("tags");
        tags.GetArrayLength().ShouldBe(2);
        tags[0].GetString().ShouldBe("readiness");
        tags[1].GetString().ShouldBe("startup");
    }

    [Fact]
    public async Task WriteAsync_WritesEmptyTags_WhenNoTagsPresent()
    {
        DefaultHttpContext httpContext = new();
        httpContext.Response.Body = new MemoryStream();

        Dictionary<string, HealthReportEntry> entries = new()
        {
            ["db"] = new HealthReportEntry(HealthStatus.Healthy, null, TimeSpan.FromMilliseconds(5), null, null, [])
        };
        HealthReport report = new(entries, TimeSpan.FromMilliseconds(5));

        await GranitHealthCheckWriter.WriteAsync(httpContext, report);

        JsonDocument json = ParseResponse(httpContext);
        JsonElement check = json.RootElement.GetProperty("checks").EnumerateArray().First();
        JsonElement tags = check.GetProperty("tags");
        tags.GetArrayLength().ShouldBe(0);
    }

    [Theory]
    [InlineData(HealthStatus.Healthy, "Healthy")]
    [InlineData(HealthStatus.Degraded, "Degraded")]
    [InlineData(HealthStatus.Unhealthy, "Unhealthy")]
    public async Task WriteAsync_MapsAllStatuses_Correctly(HealthStatus status, string expected)
    {
        DefaultHttpContext httpContext = new();
        httpContext.Response.Body = new MemoryStream();
        HealthReport report = BuildReport(status);

        await GranitHealthCheckWriter.WriteAsync(httpContext, report);

        JsonDocument json = ParseResponse(httpContext);
        json.RootElement.GetProperty("status").GetString().ShouldBe(expected);
    }

    [Fact]
    public async Task WriteAsync_SetsCacheControl_ToNoStore()
    {
        DefaultHttpContext httpContext = new();
        httpContext.Response.Body = new MemoryStream();
        HealthReport report = BuildReport(HealthStatus.Healthy);

        await GranitHealthCheckWriter.WriteAsync(httpContext, report);

        httpContext.Response.Headers.CacheControl.ToString().ShouldBe("no-store");
    }

    [Fact]
    public async Task WriteMinimalAsync_OmitsDescription_EvenWhenPresent()
    {
        DefaultHttpContext httpContext = new();
        httpContext.Response.Body = new MemoryStream();

        Dictionary<string, HealthReportEntry> entries = new()
        {
            ["db"] = new HealthReportEntry(HealthStatus.Degraded, "Connection pool exhausted", TimeSpan.FromMilliseconds(5), null, null, [])
        };
        HealthReport report = new(entries, TimeSpan.FromMilliseconds(5));

        await GranitHealthCheckWriter.WriteMinimalAsync(httpContext, report);

        JsonDocument json = ParseResponse(httpContext);
        JsonElement check = json.RootElement.GetProperty("checks").EnumerateArray().First();
        check.TryGetProperty("description", out _).ShouldBeFalse("Minimal writer should always omit description");
    }

    [Fact]
    public async Task WriteMinimalAsync_SetsCacheControl_ToNoStore()
    {
        DefaultHttpContext httpContext = new();
        httpContext.Response.Body = new MemoryStream();
        HealthReport report = BuildReport(HealthStatus.Healthy);

        await GranitHealthCheckWriter.WriteMinimalAsync(httpContext, report);

        httpContext.Response.Headers.CacheControl.ToString().ShouldBe("no-store");
    }

    [Fact]
    public async Task WriteMinimalAsync_PreservesStatusDurationAndTags()
    {
        DefaultHttpContext httpContext = new();
        httpContext.Response.Body = new MemoryStream();

        Dictionary<string, HealthReportEntry> entries = new()
        {
            ["db"] = new HealthReportEntry(HealthStatus.Healthy, "Active", TimeSpan.FromMilliseconds(12.789), null, null, ["readiness"])
        };
        HealthReport report = new(entries, TimeSpan.FromMilliseconds(12.789));

        await GranitHealthCheckWriter.WriteMinimalAsync(httpContext, report);

        JsonDocument json = ParseResponse(httpContext);
        json.RootElement.GetProperty("status").GetString().ShouldBe("Healthy");
        json.RootElement.GetProperty("duration").GetDouble().ShouldBe(12.8);

        JsonElement check = json.RootElement.GetProperty("checks").EnumerateArray().First();
        check.GetProperty("name").GetString().ShouldBe("db");
        check.GetProperty("status").GetString().ShouldBe("Healthy");
        check.GetProperty("duration").GetDouble().ShouldBe(12.8);
        check.GetProperty("tags")[0].GetString().ShouldBe("readiness");
        check.TryGetProperty("description", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task WriteAsync_UsesWebDefaults_ForCamelCasePropertyNames()
    {
        DefaultHttpContext httpContext = new();
        httpContext.Response.Body = new MemoryStream();
        HealthReport report = BuildReport(HealthStatus.Healthy);

        await GranitHealthCheckWriter.WriteAsync(httpContext, report);

        JsonDocument json = ParseResponse(httpContext);
        json.RootElement.TryGetProperty("status", out _).ShouldBeTrue("Properties should be camelCase");
        json.RootElement.TryGetProperty("duration", out _).ShouldBeTrue("Properties should be camelCase");
        json.RootElement.TryGetProperty("checks", out _).ShouldBeTrue("Properties should be camelCase");
    }

    private static HealthReport BuildReport(HealthStatus status) =>
        new(new Dictionary<string, HealthReportEntry>(), status, TimeSpan.FromMilliseconds(5));

    private static JsonDocument ParseResponse(DefaultHttpContext httpContext)
    {
        httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        return JsonDocument.Parse(httpContext.Response.Body);
    }
}
