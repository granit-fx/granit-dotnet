using Granit.DataFiltering;
using Granit.Domain;

namespace Granit.Webhooks.EntityFrameworkCore.Tests;

/// <summary>
/// Test-scoped helper that exposes a <see cref="DataFilter"/> with the
/// <see cref="IMultiTenant"/> query filter disabled for the lifetime of the test
/// instance. Tests insert and read rows under arbitrary tenant ids without
/// setting up an ambient <c>ICurrentTenant</c>, so we bypass the parameterised
/// tenant filter installed by <c>GranitDbContext</c>.
/// </summary>
/// <remarks>
/// The disable scope is captured in the test's async flow (xunit creates a fresh
/// instance per test, so the ctor and the test method share one
/// <see cref="System.Threading.ExecutionContext"/>). Dispose in the test's
/// dispose method to release the scope.
/// </remarks>
internal sealed class TestDataFilter : IDisposable
{
    public DataFilter Filter { get; } = new();
    private readonly IDisposable _scope;

    public TestDataFilter() => _scope = Filter.Disable<IMultiTenant>();

    public void Dispose() => _scope.Dispose();
}
