using System.Runtime.CompilerServices;
using Granit.Domain.ValueObjects;
using Granit.Events;
using Granit.Privacy.DataExport;
using Granit.Privacy.DataExport.Audit;
using Granit.Privacy.DataExport.Events;
using Granit.Privacy.DataExport.Fragments;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Granit.Privacy.BlobStorage.Tests;

public sealed class PrivacyFragmentUploaderTests
{
    private readonly IDistributedEventBus _eventBus = Substitute.For<IDistributedEventBus>();
    private readonly IPrivacyExportAuditWriter _auditWriter = Substitute.For<IPrivacyExportAuditWriter>();
    private readonly TimeProvider _timeProvider = TimeProvider.System;

    private PrivacyFragmentUploader CreateSut() =>
        new(_eventBus, _auditWriter, _timeProvider, NullLogger<PrivacyFragmentUploader>.Instance);

    [Fact]
    public async Task UploadAsync_ProviderYieldsNothing_PublishesEmptySentinel()
    {
        var requestId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        PersonalDataRequestedEto request = new(requestId, userId, DateTimeOffset.UtcNow, "EU_GDPR");

        StubProvider provider = new(fragments: []);

        await CreateSut().UploadAsync(request, provider, TestContext.Current.CancellationToken);

        await _eventBus.Received(1).PublishAsync(
            Arg.Is<PersonalDataPreparedEto>(e =>
                e.RequestId == requestId &&
                e.ProviderName == StubProvider.Name &&
                e.FragmentKind == PrivacyFragmentUploader.EmptyFragmentKind &&
                e.BlobReferenceId.Value == $"{PrivacyExportContainerNames.EmptyFragmentPrefix}{requestId}"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UploadAsync_StagedFragment_PublishesPreparedEto_WithBuilderProvidedData()
    {
        var requestId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        PersonalDataRequestedEto request = new(requestId, userId, DateTimeOffset.UtcNow, "EU_GDPR");

        var blobId = Guid.NewGuid();
        StagedExportFragment fragment = new()
        {
            EntryPath = "stub.json",
            ContentType = "application/json",
            KnownSizeBytes = 42,
            IntegrityTag = "v1:builder-signed",
            StagedBlob = BlobReference.Create(blobId.ToString()),
        };
        StubProvider provider = new(fragments: [fragment]);

        await CreateSut().UploadAsync(request, provider, TestContext.Current.CancellationToken);

        await _eventBus.Received(1).PublishAsync(
            Arg.Is<PersonalDataPreparedEto>(e =>
                e.RequestId == requestId &&
                e.ProviderName == StubProvider.Name &&
                e.FragmentKind == PrivacyFragmentUploader.StagedFragmentKind &&
                e.SourceContainer == PrivacyExportContainerNames.FragmentContainer &&
                e.BlobReferenceId.Value == blobId.ToString() &&
                e.EntryPath == "stub.json" &&
                e.ContentType == "application/json" &&
                e.IntegrityTag == "v1:builder-signed"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UploadAsync_PassThroughFragment_PublishesPreparedEto_WithSourceBlobReference()
    {
        var requestId = Guid.NewGuid();
        PersonalDataRequestedEto request = new(requestId, Guid.NewGuid(), DateTimeOffset.UtcNow, "EU_GDPR");

        var sourceBlob = BlobReference.Create(Guid.NewGuid().ToString());
        PassThroughExportFragment fragment = new()
        {
            EntryPath = "Documents/2024/foo.pdf",
            ContentType = "application/pdf",
            KnownSizeBytes = 1024,
            IntegrityTag = "v1:passthrough-signed",
            SourceContainer = "documents-content",
            SourceBlob = sourceBlob,
        };
        StubProvider provider = new(fragments: [fragment]);

        await CreateSut().UploadAsync(request, provider, TestContext.Current.CancellationToken);

        await _eventBus.Received(1).PublishAsync(
            Arg.Is<PersonalDataPreparedEto>(e =>
                e.FragmentKind == PrivacyFragmentUploader.PassThroughFragmentKind &&
                e.SourceContainer == "documents-content" &&
                e.BlobReferenceId == sourceBlob &&
                e.EntryPath == "Documents/2024/foo.pdf" &&
                e.ContentType == "application/pdf" &&
                e.IntegrityTag == "v1:passthrough-signed"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UploadAsync_MultipleFragments_PublishesOneEventPerFragment()
    {
        var requestId = Guid.NewGuid();
        PersonalDataRequestedEto request = new(requestId, Guid.NewGuid(), DateTimeOffset.UtcNow, "EU_GDPR");

        StagedExportFragment a = new()
        {
            EntryPath = "a.json",
            ContentType = "application/json",
            IntegrityTag = "v1:a",
            StagedBlob = BlobReference.Create(Guid.NewGuid().ToString()),
        };
        StagedExportFragment b = new()
        {
            EntryPath = "b.json",
            ContentType = "application/json",
            IntegrityTag = "v1:b",
            StagedBlob = BlobReference.Create(Guid.NewGuid().ToString()),
        };
        StubProvider provider = new(fragments: [a, b]);

        await CreateSut().UploadAsync(request, provider, TestContext.Current.CancellationToken);

        await _eventBus.Received(2).PublishAsync(
            Arg.Any<PersonalDataPreparedEto>(),
            Arg.Any<CancellationToken>());
        // No empty-sentinel when at least one fragment was yielded.
        await _eventBus.DidNotReceive().PublishAsync(
            Arg.Is<PersonalDataPreparedEto>(e => e.FragmentKind == PrivacyFragmentUploader.EmptyFragmentKind),
            Arg.Any<CancellationToken>());
    }

    private sealed class StubProvider(ExportFragment[] fragments) : IPrivacyDataProvider
    {
        public const string Name = "stub-provider";

        public static string ProviderName => Name;
        public static string DisplayKey => "Privacy.Scopes.Stub";
        public static string? FeatureName => null;

        public ValueTask<bool> HasDataAsync(PrivacyExportContext context, CancellationToken cancellationToken) =>
            ValueTask.FromResult(fragments.Length > 0);

        public async IAsyncEnumerable<ExportFragment> ExportAsync(
            PrivacyExportContext context,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            foreach (ExportFragment fragment in fragments)
            {
                await Task.Yield();
                yield return fragment;
            }
        }
    }
}
