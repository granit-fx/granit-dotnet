using System.Reflection;
using Shouldly;
using Xunit;

namespace Granit.Invoicing.Abstractions.Tests;

public sealed class InvoicingAbstractionsPackageTests
{
    [Fact]
    public void Abstractions_assembly_should_load()
    {
        Assembly assembly = typeof(Granit.Invoicing.Abstractions.Tests.InvoicingAbstractionsPackageTests).Assembly;
        assembly.ShouldNotBeNull();
    }
}
