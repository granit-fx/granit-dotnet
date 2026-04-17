using System.Globalization;
using Granit.Domain;
using Granit.ReferenceData.Domain;
using Shouldly;
using Xunit;

namespace Granit.ReferenceData.Tests;

public sealed class ReferenceDataEntityTests
{
    /// <summary>Concrete test entity to instantiate the abstract base class.</summary>
    private sealed class TestEntity : ReferenceDataEntity;

    [Fact]
    public void Inherits_AuditedEntity()
    {
        TestEntity entity = new();

        entity.ShouldBeAssignableTo<AuditedEntity>();
    }

    [Fact]
    public void Implements_IActive()
    {
        TestEntity entity = new();

        entity.ShouldBeAssignableTo<IActive>();
    }

    [Fact]
    public void Default_Activated_Is_True()
    {
        TestEntity entity = new();

        entity.Activated.ShouldBeTrue();
    }

    [Fact]
    public void Default_Code_Is_Empty()
    {
        TestEntity entity = new();

        entity.Code.ShouldBe(string.Empty);
    }

    [Fact]
    public void Default_LabelEn_Is_Empty()
    {
        TestEntity entity = new();

        entity.LabelEn.ShouldBe(string.Empty);
    }

    [Fact]
    public void Default_TranslationLabels_Are_Empty()
    {
        TestEntity entity = new();

        entity.LabelFr.ShouldBe(string.Empty);
        entity.LabelNl.ShouldBe(string.Empty);
        entity.LabelDe.ShouldBe(string.Empty);
        entity.LabelEs.ShouldBe(string.Empty);
        entity.LabelIt.ShouldBe(string.Empty);
        entity.LabelPt.ShouldBe(string.Empty);
        entity.LabelZh.ShouldBe(string.Empty);
        entity.LabelJa.ShouldBe(string.Empty);
        entity.LabelPl.ShouldBe(string.Empty);
        entity.LabelTr.ShouldBe(string.Empty);
        entity.LabelKo.ShouldBe(string.Empty);
        entity.LabelSv.ShouldBe(string.Empty);
        entity.LabelCs.ShouldBe(string.Empty);
    }

    [Fact]
    public void Label_Returns_LabelEn_By_Default()
    {
        TestEntity entity = new() { LabelEn = "Belgium" };

        entity.Label.ShouldBe("Belgium");
    }

    [Fact]
    public void Label_FrenchCulture_ReturnsLabelFr()
    {
        TestEntity entity = new() { LabelEn = "Belgium", LabelFr = "Belgique" };

        RunWithCulture("fr-BE", () => entity.Label.ShouldBe("Belgique"));
    }

    [Fact]
    public void Label_DutchCulture_ReturnsLabelNl()
    {
        TestEntity entity = new() { LabelEn = "Belgium", LabelNl = "België" };

        RunWithCulture("nl-BE", () => entity.Label.ShouldBe("België"));
    }

    [Fact]
    public void Label_GermanCulture_ReturnsLabelDe()
    {
        TestEntity entity = new() { LabelEn = "Belgium", LabelDe = "Belgien" };

        RunWithCulture("de-DE", () => entity.Label.ShouldBe("Belgien"));
    }

    [Fact]
    public void Label_SpanishCulture_ReturnsLabelEs()
    {
        TestEntity entity = new() { LabelEn = "Belgium", LabelEs = "Bélgica" };

        RunWithCulture("es-ES", () => entity.Label.ShouldBe("Bélgica"));
    }

    [Fact]
    public void Label_ItalianCulture_ReturnsLabelIt()
    {
        TestEntity entity = new() { LabelEn = "Belgium", LabelIt = "Belgio" };

        RunWithCulture("it-IT", () => entity.Label.ShouldBe("Belgio"));
    }

    [Fact]
    public void Label_PortugueseCulture_ReturnsLabelPt()
    {
        TestEntity entity = new() { LabelEn = "Belgium", LabelPt = "Bélgica" };

        RunWithCulture("pt-PT", () => entity.Label.ShouldBe("Bélgica"));
    }

    [Fact]
    public void Label_ChineseCulture_ReturnsLabelZh()
    {
        TestEntity entity = new() { LabelEn = "Belgium", LabelZh = "比利时" };

        RunWithCulture("zh-CN", () => entity.Label.ShouldBe("比利时"));
    }

    [Fact]
    public void Label_JapaneseCulture_ReturnsLabelJa()
    {
        TestEntity entity = new() { LabelEn = "Belgium", LabelJa = "ベルギー" };

        RunWithCulture("ja-JP", () => entity.Label.ShouldBe("ベルギー"));
    }

    [Fact]
    public void Label_PolishCulture_ReturnsLabelPl()
    {
        TestEntity entity = new() { LabelEn = "Belgium", LabelPl = "Belgia" };

        RunWithCulture("pl-PL", () => entity.Label.ShouldBe("Belgia"));
    }

    [Fact]
    public void Label_TurkishCulture_ReturnsLabelTr()
    {
        TestEntity entity = new() { LabelEn = "Belgium", LabelTr = "Belçika" };

        RunWithCulture("tr-TR", () => entity.Label.ShouldBe("Belçika"));
    }

    [Fact]
    public void Label_KoreanCulture_ReturnsLabelKo()
    {
        TestEntity entity = new() { LabelEn = "Belgium", LabelKo = "벨기에" };

        RunWithCulture("ko-KR", () => entity.Label.ShouldBe("벨기에"));
    }

    [Fact]
    public void Label_SwedishCulture_ReturnsLabelSv()
    {
        TestEntity entity = new() { LabelEn = "Belgium", LabelSv = "Belgien" };

        RunWithCulture("sv-SE", () => entity.Label.ShouldBe("Belgien"));
    }

    [Fact]
    public void Label_CzechCulture_ReturnsLabelCs()
    {
        TestEntity entity = new() { LabelEn = "Belgium", LabelCs = "Belgie" };

        RunWithCulture("cs-CZ", () => entity.Label.ShouldBe("Belgie"));
    }

    [Fact]
    public void Label_UnsupportedCulture_FallsBackToEnglish()
    {
        TestEntity entity = new() { LabelEn = "Belgium", LabelFr = "Belgique" };

        RunWithCulture("ar-SA", () => entity.Label.ShouldBe("Belgium"));
    }

    [Fact]
    public void Label_EmptyTranslation_FallsBackToEnglish()
    {
        TestEntity entity = new() { LabelEn = "Belgium", LabelFr = "" };

        RunWithCulture("fr-FR", () => entity.Label.ShouldBe("Belgium"));
    }

    [Fact]
    public void Default_SortOrder_Is_Zero()
    {
        TestEntity entity = new();

        entity.SortOrder.ShouldBe(0);
    }

    [Fact]
    public void Default_ValidFrom_Is_Null()
    {
        TestEntity entity = new();

        entity.ValidFrom.ShouldBeNull();
    }

    [Fact]
    public void Default_ValidTo_Is_Null()
    {
        TestEntity entity = new();

        entity.ValidTo.ShouldBeNull();
    }

    [Fact]
    public void Has_Guid_Id_From_Entity_Base()
    {
        TestEntity entity = new();

        entity.Id.ShouldBeOfType<Guid>();
    }

    [Fact]
    public void Properties_Are_Settable()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;

        TestEntity entity = new()
        {
            Code = "BE",
            LabelEn = "Belgium",
            LabelFr = "Belgique",
            LabelNl = "België",
            LabelDe = "Belgien",
            LabelEs = "Bélgica",
            LabelIt = "Belgio",
            LabelPt = "Bélgica",
            LabelZh = "比利时",
            LabelJa = "ベルギー",
            LabelPl = "Belgia",
            LabelTr = "Belçika",
            LabelKo = "벨기에",
            LabelSv = "Belgien",
            LabelCs = "Belgie",
            Activated = false,
            SortOrder = 42,
            ValidFrom = now,
            ValidTo = now.AddYears(1)
        };

        entity.Code.ShouldBe("BE");
        entity.LabelEn.ShouldBe("Belgium");
        entity.LabelFr.ShouldBe("Belgique");
        entity.LabelNl.ShouldBe("België");
        entity.LabelDe.ShouldBe("Belgien");
        entity.LabelEs.ShouldBe("Bélgica");
        entity.LabelIt.ShouldBe("Belgio");
        entity.LabelPt.ShouldBe("Bélgica");
        entity.LabelZh.ShouldBe("比利时");
        entity.LabelJa.ShouldBe("ベルギー");
        entity.LabelPl.ShouldBe("Belgia");
        entity.LabelTr.ShouldBe("Belçika");
        entity.LabelKo.ShouldBe("벨기에");
        entity.LabelSv.ShouldBe("Belgien");
        entity.LabelCs.ShouldBe("Belgie");
        entity.Activated.ShouldBeFalse();
        entity.SortOrder.ShouldBe(42);
        entity.ValidFrom.ShouldBe(now);
        entity.ValidTo.ShouldBe(now.AddYears(1));
    }

    private static void RunWithCulture(string cultureName, Action action)
    {
        CultureInfo previous = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = new CultureInfo(cultureName);
            action();
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }
}
