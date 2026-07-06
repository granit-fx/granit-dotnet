using Granit.Auditing.Domain;
using Shouldly;
using Xunit;

namespace Granit.Auditing.Tests.Domain;

public sealed class AuditPropertyChangeTests
{
    [Fact]
    public void DefaultValues_AreCorrect()
    {
        AuditPropertyChange change = new();

        change.PropertyName.ShouldBeEmpty();
        change.OriginalValue.ShouldBeNull();
        change.NewValue.ShouldBeNull();
    }
}
