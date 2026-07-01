// =============================================================================
// Tests - NotificationDefinitionContext
// =============================================================================
// Verifies the internal definition context used during startup to collect
// notification definitions from providers: Add, GetDefinitions, duplicates.
// =============================================================================

using Granit.Notifications.Internal;
using Shouldly;
using Xunit;

namespace Granit.Notifications.Tests;

public sealed class NotificationDefinitionContextTests
{
    private readonly NotificationDefinitionContext _context = new();

    [Fact]
    public void GetDefinitions_EmptyContext_ReturnsEmptyList()
    {
        IReadOnlyList<NotificationDefinition> definitions = _context.GetDefinitions();

        definitions.ShouldBeEmpty();
    }

    [Fact]
    public void Add_SingleDefinition_IsRetrievable()
    {
        NotificationDefinition definition = new("order.created")
        {
            DefaultChannels = [NotificationChannels.InApp],
            DisplayName = "Order Created",
        };

        _context.Add(definition);

        IReadOnlyList<NotificationDefinition> definitions = _context.GetDefinitions();
        definitions.ShouldHaveSingleItem();
        definitions[0].Name.ShouldBe("order.created");
        definitions[0].DisplayName.ShouldBe("Order Created");
    }

    [Fact]
    public void Add_MultipleDefinitions_ReturnsAll()
    {
        _context.Add(new NotificationDefinition("notif.one") { DefaultChannels = [NotificationChannels.InApp] });
        _context.Add(new NotificationDefinition("notif.two") { DefaultChannels = [NotificationChannels.Email] });
        _context.Add(new NotificationDefinition("notif.three") { DefaultChannels = [NotificationChannels.WebPush] });

        IReadOnlyList<NotificationDefinition> definitions = _context.GetDefinitions();

        definitions.Count.ShouldBe(3);
        definitions.ShouldContain(d => d.Name == "notif.one");
        definitions.ShouldContain(d => d.Name == "notif.two");
        definitions.ShouldContain(d => d.Name == "notif.three");
    }

    [Fact]
    public void Add_DuplicateName_BothAreStored()
    {
        // The context does not enforce uniqueness; that is handled by the store.
        _context.Add(new NotificationDefinition("duplicate.name") { DefaultChannels = [NotificationChannels.InApp] });
        _context.Add(new NotificationDefinition("duplicate.name") { DefaultChannels = [NotificationChannels.Email] });

        IReadOnlyList<NotificationDefinition> definitions = _context.GetDefinitions();

        definitions.Count.ShouldBe(2);
    }

    [Fact]
    public void GetDefinitions_PreservesInsertionOrder()
    {
        _context.Add(new NotificationDefinition("alpha") { DefaultChannels = [] });
        _context.Add(new NotificationDefinition("beta") { DefaultChannels = [] });
        _context.Add(new NotificationDefinition("gamma") { DefaultChannels = [] });

        IReadOnlyList<NotificationDefinition> definitions = _context.GetDefinitions();

        definitions[0].Name.ShouldBe("alpha");
        definitions[1].Name.ShouldBe("beta");
        definitions[2].Name.ShouldBe("gamma");
    }
}
