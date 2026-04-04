using System.Reflection;
using Shouldly;
using Xunit;

namespace Granit.Metering.Abstractions.Tests;

public sealed class MeteringAbstractionsPackageTests
{
    [Fact]
    public void Abstractions_assembly_should_load()
    {
        Assembly assembly = typeof(Granit.Metering.Abstractions.Tests.MeteringAbstractionsPackageTests).Assembly;
        assembly.ShouldNotBeNull();
    }
}
