using Granit.Http.Cookies.Domain;
using Granit.Http.Cookies.Internal;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Shouldly;
using Xunit;

namespace Granit.Http.Cookies.Tests.Ledger;

/// <summary>
/// The default ledger is a no-op but never a SILENT no-op: every swallowed decision
/// leaves a Debug log line pointing the operator to the persistent implementation.
/// </summary>
public sealed class NullConsentLedgerTests
{
    private readonly FakeLogger<NullConsentLedger> _logger = new();

    [Fact]
    public async Task RecordAsync_Completes_WithoutPersistingAnything()
    {
        // Arrange
        NullConsentLedger ledger = new(_logger);

        // Act + Assert — completes synchronously, no exception.
        await ledger.RecordAsync(CreateRecord(), TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task RecordAsync_LogsDiscardedDecision_AtDebug()
    {
        // Arrange
        NullConsentLedger ledger = new(_logger);

        // Act
        await ledger.RecordAsync(CreateRecord(), TestContext.Current.CancellationToken);

        // Assert
        FakeLogRecord log = _logger.Collector.GetSnapshot().ShouldHaveSingleItem();
        log.Level.ShouldBe(LogLevel.Debug);
        log.Message.ShouldContain("no persistent ledger is registered");
        log.Message.ShouldContain("cookieconsent");
    }

    [Fact]
    public async Task RecordAsync_WithNullRecord_Throws()
    {
        // Arrange
        NullConsentLedger ledger = new(_logger);

        // Act + Assert
        await Should.ThrowAsync<ArgumentNullException>(
            () => ledger.RecordAsync(null!, TestContext.Current.CancellationToken));
    }

    private static CookieConsentRecord CreateRecord() => CookieConsentRecord.Create(
        ["analytics"],
        ["marketing"],
        CookieConsentMode.OptIn,
        "cookieconsent",
        new DateTimeOffset(2026, 7, 14, 9, 30, 0, TimeSpan.Zero));
}
