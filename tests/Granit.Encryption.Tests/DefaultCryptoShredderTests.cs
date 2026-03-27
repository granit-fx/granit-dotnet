// =============================================================================
// DefaultCryptoShredderTests - Crypto-shredding service (GDPR Art. 17)
// =============================================================================
// Verifies:
//   - ShredAsync delegates to IEntityEncryptionKeyStore and audit recorders
//   - ShredBatchAsync iterates and delegates for each entity ID
//   - Argument validation (null, empty, whitespace)
//   - No-op behavior with empty collections and no audit recorders
// =============================================================================

using Granit.Encryption.CryptoShredding;
using Granit.Encryption.Diagnostics;
using Granit.Encryption.Services;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Encryption.Tests;

public sealed class DefaultCryptoShredderTests
{
    private readonly IEntityEncryptionKeyStore _keyStore = Substitute.For<IEntityEncryptionKeyStore>();
    private readonly ICryptoShreddingAuditRecorder _auditRecorder = Substitute.For<ICryptoShreddingAuditRecorder>();
    private readonly EncryptionMetrics _metrics;
    private readonly DefaultCryptoShredder _sut;

    public DefaultCryptoShredderTests()
    {
        _metrics = new EncryptionMetrics(new TestMeterFactory());
        _sut = new DefaultCryptoShredder(
            _keyStore,
            TimeProvider.System,
            _metrics,
            NullLogger<DefaultCryptoShredder>.Instance,
            [_auditRecorder]);
    }

    [Fact]
    public async Task ShredAsync_CallsDeleteKeyAsync()
    {
        await _sut.ShredAsync("Patient", "abc-123", TestContext.Current.CancellationToken);

        await _keyStore.Received(1).DeleteKeyAsync("Patient", "abc-123", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ShredAsync_CallsAuditRecorder()
    {
        await _sut.ShredAsync("Patient", "abc-123", TestContext.Current.CancellationToken);

        await _auditRecorder.Received(1).RecordAsync(
            "Patient",
            "abc-123",
            Arg.Any<DateTimeOffset>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ShredAsync_WithNoAuditRecorders_DoesNotThrow()
    {
        DefaultCryptoShredder shredder = new(
            _keyStore,
            TimeProvider.System,
            _metrics,
            NullLogger<DefaultCryptoShredder>.Instance,
            []);

        await Should.NotThrowAsync(() => shredder.ShredAsync("Patient", "abc-123", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ShredAsync_WithNullEntityType_ThrowsArgumentException() =>
        await Should.ThrowAsync<ArgumentException>(() => _sut.ShredAsync(null!, "abc-123", TestContext.Current.CancellationToken));

    [Fact]
    public async Task ShredAsync_WithEmptyEntityId_ThrowsArgumentException() =>
        await Should.ThrowAsync<ArgumentException>(() => _sut.ShredAsync("Patient", "", TestContext.Current.CancellationToken));

    [Fact]
    public async Task ShredBatchAsync_CallsDeleteKeyAsync_ForEachEntityId()
    {
        string[] entityIds = ["id-1", "id-2", "id-3"];

        await _sut.ShredBatchAsync("Patient", entityIds, TestContext.Current.CancellationToken);

        await _keyStore.Received(3).DeleteKeyAsync("Patient", Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _keyStore.Received(1).DeleteKeyAsync("Patient", "id-1", Arg.Any<CancellationToken>());
        await _keyStore.Received(1).DeleteKeyAsync("Patient", "id-2", Arg.Any<CancellationToken>());
        await _keyStore.Received(1).DeleteKeyAsync("Patient", "id-3", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ShredBatchAsync_CallsAuditRecorder_ForEachEntityId()
    {
        string[] entityIds = ["id-1", "id-2"];

        await _sut.ShredBatchAsync("Patient", entityIds, TestContext.Current.CancellationToken);

        await _auditRecorder.Received(2).RecordAsync(
            "Patient",
            Arg.Any<string>(),
            Arg.Any<DateTimeOffset>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ShredBatchAsync_WithEmptyEnumerable_DoesNotCallStore()
    {
        await _sut.ShredBatchAsync("Patient", [], TestContext.Current.CancellationToken);

        await _keyStore.DidNotReceive().DeleteKeyAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ShredBatchAsync_WithNullEntityType_ThrowsArgumentException() =>
        await Should.ThrowAsync<ArgumentException>(
            () => _sut.ShredBatchAsync(null!, ["id-1"], TestContext.Current.CancellationToken));

    [Fact]
    public async Task ShredBatchAsync_WithWhitespaceEntityType_ThrowsArgumentException() =>
        await Should.ThrowAsync<ArgumentException>(
            () => _sut.ShredBatchAsync("   ", ["id-1"], TestContext.Current.CancellationToken));

    [Fact]
    public async Task ShredBatchAsync_WithNullEntityIds_ThrowsArgumentNullException() =>
        await Should.ThrowAsync<ArgumentNullException>(
            () => _sut.ShredBatchAsync("Patient", null!, TestContext.Current.CancellationToken));

    [Fact]
    public async Task ShredAsync_WithWhitespaceEntityType_ThrowsArgumentException() =>
        await Should.ThrowAsync<ArgumentException>(
            () => _sut.ShredAsync("   ", "id-1", TestContext.Current.CancellationToken));

    [Fact]
    public async Task ShredAsync_WithWhitespaceEntityId_ThrowsArgumentException() =>
        await Should.ThrowAsync<ArgumentException>(
            () => _sut.ShredAsync("Patient", "   ", TestContext.Current.CancellationToken));

    /// <summary>Minimal IMeterFactory for testing.</summary>
    private sealed class TestMeterFactory : System.Diagnostics.Metrics.IMeterFactory
    {
        private readonly List<System.Diagnostics.Metrics.Meter> _meters = [];

        public System.Diagnostics.Metrics.Meter Create(System.Diagnostics.Metrics.MeterOptions options)
        {
            System.Diagnostics.Metrics.Meter meter = new(options);
            _meters.Add(meter);
            return meter;
        }

        public void Dispose()
        {
            foreach (System.Diagnostics.Metrics.Meter meter in _meters)
            {
                meter.Dispose();
            }
        }
    }
}
