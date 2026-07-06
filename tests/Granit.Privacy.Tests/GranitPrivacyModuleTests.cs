using Granit.Modularity;
using Shouldly;
using Xunit;

namespace Granit.Privacy.Tests;

public sealed class GranitPrivacyModuleTests
{
    [Fact]
    public void GranitPrivacyModule_IsGranitModule()
    {
        GranitPrivacyModule module = new();

        module.ShouldBeAssignableTo<GranitModule>();
    }

}
