using System.Reflection;
using Shouldly;
using Xunit;

namespace Granit.Payments.Abstractions.Tests;

public sealed class PaymentsAbstractionsPackageTests
{
    [Fact]
    public void Abstractions_assembly_should_load()
    {
        Assembly assembly = typeof(Granit.Payments.Abstractions.Tests.PaymentsAbstractionsPackageTests).Assembly;
        assembly.ShouldNotBeNull();
    }
}
