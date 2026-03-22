using Granit.Identity.Internal;
using Granit.Identity.Models;
using Granit.Querying;
using Shouldly;
using Xunit;

namespace Granit.Identity.Tests.Internal;

public sealed class NullUserLookupServiceTests
{
    private readonly NullUserLookupService _service = new();

    // -------------------------------------------------------------------------
    // Query methods
    // -------------------------------------------------------------------------

    [Fact]
    public async Task FindByIdAsync_ReturnsNull()
    {
        IIdentityUser? result = await _service.FindByIdAsync(
            "user-1", TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task FindByIdsAsync_ReturnsEmptyList()
    {
        IReadOnlyList<IIdentityUser> result = await _service.FindByIdsAsync(
            ["user-1", "user-2"], TestContext.Current.CancellationToken);

        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task SearchAsync_ReturnsEmptyPagedResult()
    {
        PagedResult<IIdentityUser> result = await _service.SearchAsync(
            "alice", cancellationToken: TestContext.Current.CancellationToken);

        result.Items.ShouldBeEmpty();
        result.TotalCount.ShouldBe(0);
        result.HasMore.ShouldBeFalse();
    }

    // -------------------------------------------------------------------------
    // Refresh methods
    // -------------------------------------------------------------------------

    [Fact]
    public async Task RefreshByIdAsync_ReturnsNull()
    {
        IIdentityUser? result = await _service.RefreshByIdAsync(
            "user-1", TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task RefreshAllAsync_ReturnsZero()
    {
        int result = await _service.RefreshAllAsync(TestContext.Current.CancellationToken);

        result.ShouldBe(0);
    }

    [Fact]
    public async Task RefreshStaleAsync_ReturnsZero()
    {
        int result = await _service.RefreshStaleAsync(TestContext.Current.CancellationToken);

        result.ShouldBe(0);
    }

    // -------------------------------------------------------------------------
    // Write methods (GDPR)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task DeleteByIdAsync_CompletesWithoutThrowing()
    {
        Func<Task> act = () => _service.DeleteByIdAsync(
            "user-1", TestContext.Current.CancellationToken);

        await Should.NotThrowAsync(act);
    }

    [Fact]
    public async Task PseudonymizeByIdAsync_CompletesWithoutThrowing()
    {
        Func<Task> act = () => _service.PseudonymizeByIdAsync(
            "user-1", TestContext.Current.CancellationToken);

        await Should.NotThrowAsync(act);
    }

    [Fact]
    public void ImplementsIUserLookupService() =>
        _service.ShouldBeAssignableTo<IUserLookupService>();
}
