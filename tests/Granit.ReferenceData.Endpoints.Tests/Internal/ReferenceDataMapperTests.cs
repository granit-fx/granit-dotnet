using Granit.ReferenceData.Domain;
using Granit.ReferenceData.Endpoints.Dtos;
using Granit.ReferenceData.Endpoints.Internal;
using Shouldly;
using Xunit;

namespace Granit.ReferenceData.Endpoints.Tests.Internal;

public sealed class ReferenceDataMapperTests
{
    private sealed class TestEntity : ReferenceDataEntity;

    [Fact]
    public void ToResponse_maps_all_fields()
    {
        var id = Guid.NewGuid();
        DateTimeOffset validFrom = DateTimeOffset.UtcNow;
        DateTimeOffset validTo = validFrom.AddYears(1);

        TestEntity entity = new()
        {
            Id = id,
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
            IsActive = true,
            SortOrder = 5,
            ValidFrom = validFrom,
            ValidTo = validTo,
            ParentCode = "EU",
        };

        ReferenceDataResponse response = ReferenceDataMapper.ToResponse(entity);

        response.Id.ShouldBe(id);
        response.Code.ShouldBe("BE");
        response.LabelEn.ShouldBe("Belgium");
        response.LabelFr.ShouldBe("Belgique");
        response.LabelNl.ShouldBe("België");
        response.LabelDe.ShouldBe("Belgien");
        response.LabelEs.ShouldBe("Bélgica");
        response.LabelIt.ShouldBe("Belgio");
        response.LabelPt.ShouldBe("Bélgica");
        response.LabelZh.ShouldBe("比利时");
        response.LabelJa.ShouldBe("ベルギー");
        response.LabelPl.ShouldBe("Belgia");
        response.LabelTr.ShouldBe("Belçika");
        response.LabelKo.ShouldBe("벨기에");
        response.LabelSv.ShouldBe("Belgien");
        response.LabelCs.ShouldBe("Belgie");
        response.IsActive.ShouldBeTrue();
        response.SortOrder.ShouldBe(5);
        response.ValidFrom.ShouldBe(validFrom);
        response.ValidTo.ShouldBe(validTo);
        response.ParentCode.ShouldBe("EU");
    }

    [Fact]
    public void ToResponse_with_no_extra_properties_sets_null()
    {
        TestEntity entity = new()
        {
            Code = "BE",
            LabelEn = "Belgium",
        };

        ReferenceDataResponse response = ReferenceDataMapper.ToResponse(entity);

        response.ExtraProperties.ShouldBeNull();
    }

    [Fact]
    public void ToResponse_with_extra_properties_returns_dictionary()
    {
        TestEntity entity = new()
        {
            Code = "BE",
            LabelEn = "Belgium",
            ExtraPropertiesJson = """{"Alpha3Code":"BEL","Population":"11000000"}""",
        };

        ReferenceDataResponse response = ReferenceDataMapper.ToResponse(entity);

        response.ExtraProperties.ShouldNotBeNull();
        response.ExtraProperties!.Count.ShouldBe(2);
        response.ExtraProperties["Alpha3Code"].ShouldBe("BEL");
        response.ExtraProperties["Population"].ShouldBe("11000000");
    }

    [Fact]
    public void ToResponse_with_empty_json_bag_sets_null()
    {
        TestEntity entity = new()
        {
            Code = "BE",
            LabelEn = "Belgium",
            ExtraPropertiesJson = "{}",
        };

        ReferenceDataResponse response = ReferenceDataMapper.ToResponse(entity);

        response.ExtraProperties.ShouldBeNull();
    }

    [Fact]
    public void ToResponse_nullable_fields_default_to_null()
    {
        TestEntity entity = new()
        {
            Code = "XX",
            LabelEn = "Test",
        };

        ReferenceDataResponse response = ReferenceDataMapper.ToResponse(entity);

        response.ValidFrom.ShouldBeNull();
        response.ValidTo.ShouldBeNull();
        response.ParentCode.ShouldBeNull();
    }
}
