using System.Reflection;
using System.Reflection.Emit;
using Granit.Reflection;
using Shouldly;
using Xunit;

namespace Granit.Tests;

/// <summary>
/// Verifies <see cref="SafeTypeLoader"/> returns the loadable subset instead of throwing when an
/// assembly references an unloaded dependency — the failure mode that crashed framework-wide
/// discovery scans (see issue #2541 / #2540).
/// </summary>
public sealed class SafeTypeLoaderTests
{
    [Fact]
    public void GetLoadableTypes_on_a_real_assembly_returns_its_types()
    {
        IReadOnlyList<Type> types = typeof(SafeTypeLoader).Assembly.GetLoadableTypes();

        types.ShouldNotBeEmpty();
        types.ShouldContain(typeof(SafeTypeLoader));
    }

    [Fact]
    public void GetLoadableExportedTypes_returns_only_public_types()
    {
        IReadOnlyList<Type> types = typeof(SafeTypeLoader).Assembly.GetLoadableExportedTypes();

        types.ShouldNotBeEmpty();
        types.ShouldAllBe(t => t.IsPublic || t.IsNestedPublic);
    }

    [Fact]
    public void Dynamic_assemblies_return_empty()
    {
        var dynamicAssembly = AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName("Granit.Tests.Dynamic"), AssemblyBuilderAccess.Run);

        dynamicAssembly.IsDynamic.ShouldBeTrue();
        dynamicAssembly.GetLoadableTypes().ShouldBeEmpty();
        dynamicAssembly.GetLoadableExportedTypes().ShouldBeEmpty();
    }

    [Fact]
    public void ReflectionTypeLoadException_returns_the_partial_loadable_set()
    {
        IReadOnlyList<Type> result = SafeTypeLoader.Load(
            typeof(SafeTypeLoader).Assembly,
            _ => throw new ReflectionTypeLoadException(
                [typeof(int), null, typeof(string)],
                new Exception?[3]));

        result.ShouldBe(new[] { typeof(int), typeof(string) });
    }

    [Theory]
    [MemberData(nameof(MissingDependencyExceptions))]
    public void Missing_dependency_failures_return_empty(Exception exception)
    {
        IReadOnlyList<Type> result = SafeTypeLoader.Load(
            typeof(SafeTypeLoader).Assembly, _ => throw exception);

        result.ShouldBeEmpty();
    }

    public static TheoryData<Exception> MissingDependencyExceptions() => new()
    {
        new FileNotFoundException(),
        new FileLoadException(),
        new TypeLoadException(),
    };

    [Fact]
    public void Unexpected_exceptions_propagate()
    {
        Should.Throw<InvalidOperationException>(() => SafeTypeLoader.Load(
            typeof(SafeTypeLoader).Assembly, _ => throw new InvalidOperationException("boom")));
    }

    [Fact]
    public void Null_assembly_throws()
    {
        Should.Throw<ArgumentNullException>(() => ((Assembly)null!).GetLoadableTypes());
    }
}
