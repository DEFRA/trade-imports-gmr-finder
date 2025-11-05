using AutoFixture;
using AutoFixture.Dsl;
using Defra.TradeImportsDataApi.Domain.CustomsDeclaration;
using Defra.TradeImportsDataApi.Domain.Events;

namespace TestFixtures;

public static class CustomsDeclarationFixtures
{
    public static IPostprocessComposer<ResourceEvent<CustomsDeclaration>> CustomsDeclarationResourceEventFixture(
        CustomsDeclaration customsDeclaration
    )
    {
        return GetFixture()
            .Build<ResourceEvent<CustomsDeclaration>>()
            .With(x => x.Resource, customsDeclaration)
            .With(x => x.ResourceType, ResourceEventResourceTypes.CustomsDeclaration);
    }

    public static IPostprocessComposer<CustomsDeclaration> CustomsDeclarationFixture()
    {
        return GetFixture()
            .Build<CustomsDeclaration>()
            .With(x => x.ClearanceRequest, ClearanceRequestFixture().Create())
            .With(
                x => x.ClearanceDecision,
                ClearanceDecisionFixture(["CHEDPP.GB.2025.1053368", "CHEDA.GB.2025.1251361"]).Create()
            );
    }

    public static IPostprocessComposer<ClearanceDecision> ClearanceDecisionFixture(HashSet<string> chedReferences)
    {
        var results = chedReferences
            .Select(ched =>
                GetFixture().Build<ClearanceDecisionResult>().With(x => x.ImportPreNotification, ched).Create()
            )
            .ToArray();

        return GetFixture().Build<ClearanceDecision>().With(x => x.Results, results);
    }

    public static IPostprocessComposer<ClearanceRequest> ClearanceRequestFixture()
    {
        return GetFixture().Build<ClearanceRequest>().With(x => x.GoodsLocationCode, "POOPOOPOOGVM");
    }

    private static Fixture GetFixture()
    {
        var fixture = new Fixture();
        fixture.Customize<DateOnly>(o => o.FromFactory((DateTime dt) => DateOnly.FromDateTime(dt)));
        return fixture;
    }
}
