using System;
using System.Threading;
using System.Threading.Tasks;
using Granit.Documents;
using Granit.Documents.AssetMetadata;
using Granit.Documents.AssetMetadata.Domain;
using Granit.Documents.AssetMetadata.EntityFrameworkCore.Internal;
using Granit.Documents.Domain;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Documents.AssetMetadata.EntityFrameworkCore.Tests;

public sealed class AssetMetadataServiceTests
{
    private readonly IDocumentService _documents = Substitute.For<IDocumentService>();
    private readonly IAssetMetadataStore _store = Substitute.For<IAssetMetadataStore>();
    private readonly AssetMetadataService _sut;

    public AssetMetadataServiceTests() => _sut = new AssetMetadataService(_documents, _store);

    [Fact]
    public async Task GetForCurrentVersionAsync_returns_null_when_document_missing()
    {
        var documentId = Guid.NewGuid();
        _documents.GetByIdAsync(documentId, Arg.Any<CancellationToken>()).Returns((Document?)null);

        DocumentAssetMetadata? result = await _sut.GetForCurrentVersionAsync(
            documentId, TestContext.Current.CancellationToken);

        result.ShouldBeNull();
        await _store.DidNotReceiveWithAnyArgs().GetByVersionAsync(
            Guid.Empty, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task GetForVersionAsync_returns_null_when_document_missing()
    {
        var documentId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        _documents.GetByIdAsync(documentId, Arg.Any<CancellationToken>()).Returns((Document?)null);

        DocumentAssetMetadata? result = await _sut.GetForVersionAsync(
            documentId, versionId, TestContext.Current.CancellationToken);

        result.ShouldBeNull();
        await _store.DidNotReceiveWithAnyArgs().GetByVersionAsync(
            Guid.Empty, TestContext.Current.CancellationToken);
    }
}
