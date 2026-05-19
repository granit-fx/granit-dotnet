using Granit.Privacy.LegalAgreements;
using Granit.Privacy.LegalAgreements.Domain;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Granit.Privacy.Tests;

public sealed class GranitPrivacyBuilderTests
{
    [Fact]
    public void RegisterDataProvider_AddsProviderName()
    {
        ServiceCollection services = new();
        GranitPrivacyBuilder builder = new(services);

        GranitPrivacyBuilder result = builder.RegisterDataProvider("patients");

        result.ShouldBe(builder);
        builder.DataProviderNames.ShouldContain("patients");
    }

    [Fact]
    public void RegisterDataProvider_MultipleProviders_AddsAll()
    {
        ServiceCollection services = new();
        GranitPrivacyBuilder builder = new(services);

        builder.RegisterDataProvider("patients")
            .RegisterDataProvider("billing")
            .RegisterDataProvider("appointments");

        builder.DataProviderNames.Count.ShouldBe(3);
    }

    [Fact]
    public void RegisterDataProvider_NullName_ThrowsArgumentException()
    {
        ServiceCollection services = new();
        GranitPrivacyBuilder builder = new(services);

        Action act = () => builder.RegisterDataProvider(null!);

        Should.Throw<ArgumentException>(act);
    }

    [Fact]
    public void RegisterDataProvider_EmptyName_ThrowsArgumentException()
    {
        ServiceCollection services = new();
        GranitPrivacyBuilder builder = new(services);

        Action act = () => builder.RegisterDataProvider("");

        Should.Throw<ArgumentException>(act);
    }

    [Fact]
    public void RegisterDataProvider_WhitespaceName_ThrowsArgumentException()
    {
        ServiceCollection services = new();
        GranitPrivacyBuilder builder = new(services);

        Action act = () => builder.RegisterDataProvider("   ");

        Should.Throw<ArgumentException>(act);
    }

    [Fact]
    public void RegisterDocument_AddsLegalDocumentDefinition()
    {
        ServiceCollection services = new();
        GranitPrivacyBuilder builder = new(services);

        GranitPrivacyBuilder result = builder.RegisterDocument("privacy-policy", "1.0.0", "Privacy Policy");

        result.ShouldBe(builder);
        builder.LegalDocuments.Count.ShouldBe(1);
        builder.LegalDocuments[0].DocumentId.ShouldBe("privacy-policy");
        builder.LegalDocuments[0].CurrentVersion.ShouldBe("1.0.0");
        builder.LegalDocuments[0].DisplayName.ShouldBe("Privacy Policy");
    }

    [Fact]
    public void RegisterDocument_MultipleDocuments_AddsAll()
    {
        ServiceCollection services = new();
        GranitPrivacyBuilder builder = new(services);

        builder.RegisterDocument("privacy-policy", "1.0.0", "Privacy Policy")
            .RegisterDocument("terms", "2.0.0", "Terms of Service");

        builder.LegalDocuments.Count.ShouldBe(2);
    }

    [Fact]
    public void UseLegalAgreementStore_RegistersBothReaderAndWriter()
    {
        ServiceCollection services = new();
        GranitPrivacyBuilder builder = new(services);

        GranitPrivacyBuilder result = builder.UseLegalAgreementStore<TestLegalAgreementStore>();

        result.ShouldBe(builder);

        ServiceProvider provider = services.BuildServiceProvider();
        provider.GetService<ILegalAgreementStoreReader>().ShouldNotBeNull();
        provider.GetService<ILegalAgreementStoreWriter>().ShouldNotBeNull();
    }

    [Fact]
    public void UseLegalAgreementStore_ReaderAndWriterResolveSameInstance()
    {
        ServiceCollection services = new();
        GranitPrivacyBuilder builder = new(services);
        builder.UseLegalAgreementStore<TestLegalAgreementStore>();

        ServiceProvider provider = services.BuildServiceProvider();
        using IServiceScope scope = provider.CreateScope();
        ILegalAgreementStoreReader reader = scope.ServiceProvider.GetRequiredService<ILegalAgreementStoreReader>();
        ILegalAgreementStoreWriter writer = scope.ServiceProvider.GetRequiredService<ILegalAgreementStoreWriter>();

        reader.ShouldBeSameAs(writer);
    }

    [Fact]
    public void Services_ExposesUnderlyingServiceCollection()
    {
        ServiceCollection services = new();
        GranitPrivacyBuilder builder = new(services);

        builder.Services.ShouldBe(services);
    }

    [Fact]
    public void FluentChaining_AllMethodsReturnSameBuilder()
    {
        ServiceCollection services = new();
        GranitPrivacyBuilder builder = new(services);

        GranitPrivacyBuilder result = builder
            .RegisterDataProvider("patients")
            .RegisterDocument("privacy-policy", "1.0.0", "Privacy Policy")
            .UseLegalAgreementStore<TestLegalAgreementStore>();

        result.ShouldBe(builder);
    }

    private sealed class TestLegalAgreementStore : ILegalAgreementStoreReader, ILegalAgreementStoreWriter
    {
        public Task<LegalAgreementBase?> FindLatestAsync(Guid userId, string documentId, CancellationToken cancellationToken = default)
            => Task.FromResult<LegalAgreementBase?>(null);

        public Task<IReadOnlyList<LegalAgreementBase>> FindAllByUserAsync(Guid userId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<LegalAgreementBase>>([]);

        public Task RecordAsync(LegalAgreementBase agreement, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task RecordConsentAsync(Guid userId, string documentId, string version, string? ipAddress, DateTimeOffset acceptedAt, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public async IAsyncEnumerable<Guid> StreamUsersByDocumentVersionAsync(
            string documentId, string version, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask.ConfigureAwait(false);
            yield break;
        }
    }
}
