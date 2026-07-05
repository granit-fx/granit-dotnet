using Granit.Privacy.DataExport;
using Granit.Privacy.Extensions;
using Granit.Privacy.LegalAgreements;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Privacy.Tests;

public sealed class PrivacyServiceCollectionExtensionsTests
{
    [Fact]
    public void AddGranitPrivacy_RegistersServices()
    {
        ServiceCollection services = new();
        services.AddScoped(_ => Substitute.For<ILegalAgreementStoreReader>());
        services.AddGranitPrivacy(privacy =>
        {
            privacy.RegisterDataProvider("patients");
            privacy.RegisterDocument("privacy-policy", "1.0.0", "Privacy Policy");
        });

        ServiceProvider provider = services.BuildServiceProvider();

        provider.GetService<IDataProviderRegistry>().ShouldNotBeNull();
        provider.GetService<ILegalDocumentRegistry>().ShouldNotBeNull();
        provider.GetService<ILegalAgreementChecker>().ShouldNotBeNull();
    }

    [Fact]
    public void AddGranitPrivacy_RegistersDataProviders()
    {
        ServiceCollection services = new();
        services.AddGranitPrivacy(privacy =>
        {
            privacy.RegisterDataProvider("patients");
            privacy.RegisterDataProvider("billing");
        });

        ServiceProvider provider = services.BuildServiceProvider();
        IDataProviderRegistry? registry = provider.GetService<IDataProviderRegistry>();

        registry.ShouldNotBeNull();
        registry!.Count.ShouldBe(2);
        registry.GetAll().ShouldContain("patients");
        registry.GetAll().ShouldContain("billing");
    }

    [Fact]
    public void AddGranitPrivacy_RegistersLegalDocuments()
    {
        ServiceCollection services = new();
        services.AddGranitPrivacy(privacy =>
        {
            privacy.RegisterDocument("privacy-policy", "2.0.0", "Privacy Policy");
            privacy.RegisterDocument("terms", "1.0.0", "Terms of Service");
        });

        ServiceProvider provider = services.BuildServiceProvider();
        ILegalDocumentRegistry? registry = provider.GetService<ILegalDocumentRegistry>();

        registry.ShouldNotBeNull();
        registry!.GetAll().Count.ShouldBe(2);
        registry.GetDefinition("privacy-policy")!.CurrentVersion.ShouldBe("2.0.0");
    }

    [Fact]
    public void AddGranitPrivacy_NullConfigure_ThrowsArgumentNullException()
    {
        ServiceCollection services = new();

        Action act = () => services.AddGranitPrivacy(null!);

        Should.Throw<ArgumentNullException>(act);
    }

    [Fact]
    public void AddGranitPrivacy_WithoutLegalAgreementStore_DoesNotRegisterChecker()
    {
        ServiceCollection services = new();
        services.AddGranitPrivacy(privacy => privacy.RegisterDocument("privacy-policy", "1.0.0", "Privacy Policy"));

        ServiceProvider provider = services.BuildServiceProvider();

        provider.GetService<ILegalAgreementChecker>().ShouldBeNull();
    }

    [Fact]
    public void AddGranitPrivacy_WithNoProvidersOrDocuments_RegistersCoreServices()
    {
        ServiceCollection services = new();
        services.AddGranitPrivacy(_ => { });

        ServiceProvider provider = services.BuildServiceProvider();

        provider.GetService<IDataProviderRegistry>().ShouldNotBeNull();
        provider.GetService<ILegalDocumentRegistry>().ShouldNotBeNull();
    }

    [Fact]
    public void AddGranitPrivacy_RegistersPrivacyMetrics()
    {
        ServiceCollection services = new();
        services.AddMetrics();
        services.AddGranitPrivacy(_ => { });

        ServiceProvider provider = services.BuildServiceProvider();

        provider.GetService<Granit.Privacy.Diagnostics.PrivacyMetrics>().ShouldNotBeNull();
    }

    [Fact]
    public void AddGranitPrivacy_CalledTwice_DoesNotDuplicateSingletons()
    {
        ServiceCollection services = new();
        services.AddGranitPrivacy(p => p.RegisterDataProvider("a"));
        services.AddGranitPrivacy(p => p.RegisterDataProvider("b"));

        ServiceProvider provider = services.BuildServiceProvider();

        // TryAddSingleton ensures only the first registration wins
        IDataProviderRegistry registry = provider.GetRequiredService<IDataProviderRegistry>();
        registry.Count.ShouldBe(1);
        registry.GetAll().ShouldContain("a");
    }
}
