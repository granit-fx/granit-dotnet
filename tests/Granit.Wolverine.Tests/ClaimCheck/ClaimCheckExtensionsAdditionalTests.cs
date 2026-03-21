// =============================================================================
// Tests - ClaimCheckExtensions (additional coverage)
// =============================================================================
// Covers null guard tests for StorePayloadAsync and RetrievePayloadAsync.
// =============================================================================

using Granit.Wolverine.ClaimCheck;
using Shouldly;
using Xunit;

namespace Granit.Wolverine.Tests.ClaimCheck;

public sealed class ClaimCheckExtensionsAdditionalTests
{
    [Fact]
    public async Task StorePayloadAsync_WithNullStore_ThrowsArgumentNullException()
    {
        IClaimCheckStore store = null!;

        Func<Task> act = () => store.StorePayloadAsync(
            new SamplePayload("test"), cancellationToken: TestContext.Current.CancellationToken);

        ArgumentNullException exception = await Should.ThrowAsync<ArgumentNullException>(act);
        exception.ParamName.ShouldBe("store");
    }

    [Fact]
    public async Task StorePayloadAsync_WithNullPayload_ThrowsArgumentNullException()
    {
        IClaimCheckStore store = NSubstitute.Substitute.For<IClaimCheckStore>();

        Func<Task> act = () => store.StorePayloadAsync<SamplePayload>(
            null!, cancellationToken: TestContext.Current.CancellationToken);

        ArgumentNullException exception = await Should.ThrowAsync<ArgumentNullException>(act);
        exception.ParamName.ShouldBe("payload");
    }

    [Fact]
    public async Task RetrievePayloadAsync_WithNullStore_ThrowsArgumentNullException()
    {
        IClaimCheckStore store = null!;
        var reference = ClaimCheckReference.Create<SamplePayload>(Guid.NewGuid());

        Func<Task> act = () => store.RetrievePayloadAsync<SamplePayload>(
            reference, TestContext.Current.CancellationToken);

        ArgumentNullException exception = await Should.ThrowAsync<ArgumentNullException>(act);
        exception.ParamName.ShouldBe("store");
    }

    [Fact]
    public async Task RetrievePayloadAsync_WithNullReference_ThrowsArgumentNullException()
    {
        IClaimCheckStore store = NSubstitute.Substitute.For<IClaimCheckStore>();

        Func<Task> act = () => store.RetrievePayloadAsync<SamplePayload>(
            null!, TestContext.Current.CancellationToken);

        ArgumentNullException exception = await Should.ThrowAsync<ArgumentNullException>(act);
        exception.ParamName.ShouldBe("reference");
    }

    private sealed record SamplePayload(string Name);
}
