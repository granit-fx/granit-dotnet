using Granit.Documents.Extensions;
using Granit.Documents.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Granit.Documents.Tests;

public sealed class GranitDocumentsServiceCollectionExtensionsTests
{
    private static ServiceCollection NewServices()
    {
        ServiceCollection services = new();
        // BindConfiguration requires an IConfiguration in DI — provide an empty one
        // so unit tests can resolve options without a host builder.
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        return services;
    }

    [Fact]
    public void AddGranitDocuments_RegistersOptions_WithDefaultValues()
    {
        ServiceCollection services = NewServices();

        services.AddGranitDocuments();

        ServiceProvider provider = services.BuildServiceProvider();
        GranitDocumentsOptions options = provider.GetRequiredService<IOptions<GranitDocumentsOptions>>().Value;

        options.TrashRetentionDays.ShouldBe(30);
        options.DefaultTenantQuotaBytes.ShouldBe(5L * 1024L * 1024L * 1024L);
        options.AclCacheTtl.ShouldBe(TimeSpan.FromMinutes(5));
    }

    [Fact]
    public void AddGranitDocuments_AppliesConfigureCallback_OverridingDefaults()
    {
        ServiceCollection services = NewServices();

        services.AddGranitDocuments(o =>
        {
            o.TrashRetentionDays = 7;
            o.DefaultTenantQuotaBytes = 1024L * 1024L * 1024L; // 1 GB
            o.AclCacheTtl = TimeSpan.FromMinutes(2);
        });

        ServiceProvider provider = services.BuildServiceProvider();
        GranitDocumentsOptions options = provider.GetRequiredService<IOptions<GranitDocumentsOptions>>().Value;

        options.TrashRetentionDays.ShouldBe(7);
        options.DefaultTenantQuotaBytes.ShouldBe(1024L * 1024L * 1024L);
        options.AclCacheTtl.ShouldBe(TimeSpan.FromMinutes(2));
    }

    [Fact]
    public void AddGranitDocuments_NullServices_Throws()
    {
        IServiceCollection? services = null;

        Should.Throw<ArgumentNullException>(() => services!.AddGranitDocuments());
    }
}
