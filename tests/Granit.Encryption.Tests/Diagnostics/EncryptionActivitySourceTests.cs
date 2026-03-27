// =============================================================================
// EncryptionActivitySourceTests - Distributed tracing for encryption module
// =============================================================================
// Verifies:
//   - ActivitySource name and singleton instance are valid
//   - Operation name constants are non-empty and produce activities
//   - Activities carry expected operation names when a listener is attached
// =============================================================================

using System.Diagnostics;
using Granit.Encryption.Diagnostics;
using Shouldly;
using Xunit;

namespace Granit.Encryption.Tests.Diagnostics;

public sealed class EncryptionActivitySourceTests : IDisposable
{
    private readonly ActivityListener _listener;

    public EncryptionActivitySourceTests()
    {
        _listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == EncryptionActivitySource.Name,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) =>
                ActivitySamplingResult.AllDataAndRecorded,
        };
        ActivitySource.AddActivityListener(_listener);
    }

    public void Dispose() => _listener.Dispose();

    // ──── Name and Source instance ────

    [Fact]
    public void Name_IsGranitEncryption() =>
        EncryptionActivitySource.Name.ShouldBe("Granit.Encryption");

    [Fact]
    public void Source_IsNotNull() =>
        EncryptionActivitySource.Source.ShouldNotBeNull();

    [Fact]
    public void Source_Name_MatchesConstant() =>
        EncryptionActivitySource.Source.Name.ShouldBe(EncryptionActivitySource.Name);

    // ──── Operation name constants ────

    [Fact]
    public void KeyCreate_IsNotNullOrEmpty() =>
        EncryptionActivitySource.KeyCreate.ShouldNotBeNullOrWhiteSpace();

    [Fact]
    public void KeyRetrieve_IsNotNullOrEmpty() =>
        EncryptionActivitySource.KeyRetrieve.ShouldNotBeNullOrWhiteSpace();

    [Fact]
    public void KeyDelete_IsNotNullOrEmpty() =>
        EncryptionActivitySource.KeyDelete.ShouldNotBeNullOrWhiteSpace();

    [Fact]
    public void Shred_IsNotNullOrEmpty() =>
        EncryptionActivitySource.Shred.ShouldNotBeNullOrWhiteSpace();

    [Fact]
    public void ShredBatch_IsNotNullOrEmpty() =>
        EncryptionActivitySource.ShredBatch.ShouldNotBeNullOrWhiteSpace();

    // ──── Activities are created when listener is attached ────

    [Fact]
    public void StartActivity_KeyCreate_ReturnsActivity()
    {
        using Activity? activity = EncryptionActivitySource.Source.StartActivity(
            EncryptionActivitySource.KeyCreate);

        activity.ShouldNotBeNull();
        activity.OperationName.ShouldBe("encryption.key-create");
    }

    [Fact]
    public void StartActivity_KeyRetrieve_ReturnsActivity()
    {
        using Activity? activity = EncryptionActivitySource.Source.StartActivity(
            EncryptionActivitySource.KeyRetrieve);

        activity.ShouldNotBeNull();
        activity.OperationName.ShouldBe("encryption.key-retrieve");
    }

    [Fact]
    public void StartActivity_KeyDelete_ReturnsActivity()
    {
        using Activity? activity = EncryptionActivitySource.Source.StartActivity(
            EncryptionActivitySource.KeyDelete);

        activity.ShouldNotBeNull();
        activity.OperationName.ShouldBe("encryption.key-delete");
    }

    [Fact]
    public void StartActivity_Shred_ReturnsActivity()
    {
        using Activity? activity = EncryptionActivitySource.Source.StartActivity(
            EncryptionActivitySource.Shred);

        activity.ShouldNotBeNull();
        activity.OperationName.ShouldBe("encryption.shred");
    }

    [Fact]
    public void StartActivity_ShredBatch_ReturnsActivity()
    {
        using Activity? activity = EncryptionActivitySource.Source.StartActivity(
            EncryptionActivitySource.ShredBatch);

        activity.ShouldNotBeNull();
        activity.OperationName.ShouldBe("encryption.shred-batch");
    }
}
