using Granit.Persistence.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace Granit.ArchitectureTests;

/// <summary>Serialises every test class that flips the native-conventions switch or builds snapshot models.</summary>
[CollectionDefinition(Name)]
public sealed class ModelSnapshotSerialGroup
{
    /// <summary>Collection name.</summary>
    public const string Name = "model-snapshots";
}

/// <summary>
/// Phase 2 parity gate (#3158): every DbContext builds its model twice — legacy
/// <c>ApplyGranitConventionsCore</c> pipeline vs the native convention engine
/// (<c>Granit.Persistence.NativeConventions</c> AppContext switch) — and the two snapshots
/// (full debug string + named filter expressions) must be bit-identical. The flip (#3159)
/// happens only when this suite is green for all contexts.
/// </summary>
[Collection(ModelSnapshotSerialGroup.Name)]
public sealed class ModelSnapshotNativeParityTests
{
    private const string SwitchName = "Granit.Persistence.NativeConventions";

    public static TheoryData<string> GranitContexts()
    {
        TheoryData<string> data = [];
        foreach (Type type in ModelSnapshotTests.EnumerateContextTypes()
            .Where(t => typeof(GranitDbContext).IsAssignableFrom(t)))
        {
            data.Add(type.FullName!);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(GranitContexts))]
    public void Native_conventions_produce_an_identical_model(string contextTypeName)
    {
        Type contextType = ModelSnapshotTests.EnumerateContextTypes()
            .Single(t => t.FullName == contextTypeName);

        string legacy = ModelSnapshotTests.BuildSnapshot(contextType, freshInternalServices: true);

        AppContext.SetSwitch(SwitchName, true);
        try
        {
            string native = ModelSnapshotTests.BuildSnapshot(contextType, freshInternalServices: true);
            native.ShouldBe(legacy,
                $"{contextTypeName}: the native convention engine produced a different model "
                + "than the legacy ApplyGranitConventionsCore pipeline.");
        }
        finally
        {
            AppContext.SetSwitch(SwitchName, false);
        }
    }
}
