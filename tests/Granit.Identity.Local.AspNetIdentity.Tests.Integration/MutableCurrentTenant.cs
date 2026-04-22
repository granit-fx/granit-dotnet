using Granit.MultiTenancy;

namespace Granit.Identity.Local.AspNetIdentity.Tests.Integration;

/// <summary>
/// Test-only <see cref="ICurrentTenant"/> whose state can be flipped between tests
/// to drive the host/tenant visibility scenarios. Registered as a singleton so
/// each test can mutate the active tenant without rebuilding the service provider.
/// </summary>
public sealed class MutableCurrentTenant : ICurrentTenant
{
    public bool IsAvailable => Id is not null;

    public Guid? Id { get; private set; }

    public string? Name { get; private set; }

    public IDisposable Change(Guid? id, string? name = null)
    {
        Guid? previousId = Id;
        string? previousName = Name;
        Id = id;
        Name = name;
        return new Scope(this, previousId, previousName);
    }

    private sealed class Scope(MutableCurrentTenant owner, Guid? previousId, string? previousName) : IDisposable
    {
        public void Dispose()
        {
            owner.Id = previousId;
            owner.Name = previousName;
        }
    }
}
