using Granit.MultiTenancy;

namespace Granit.IO.Tests;

/// <summary>Minimal in-process <see cref="ICurrentTenant"/> for tests.</summary>
internal sealed class FakeCurrentTenant(Guid? id = null, string? name = null) : ICurrentTenant
{
    public bool IsAvailable => Id.HasValue;

    public Guid? Id { get; private set; } = id;

    public string? Name { get; private set; } = name;

    public IDisposable Change(Guid? id, string? name = null)
    {
        Guid? previousId = Id;
        string? previousName = Name;
        Id = id;
        Name = name;
        return new Scope(this, previousId, previousName);
    }

    private sealed class Scope(FakeCurrentTenant owner, Guid? prevId, string? prevName) : IDisposable
    {
        public void Dispose()
        {
            owner.Id = prevId;
            owner.Name = prevName;
        }
    }
}
