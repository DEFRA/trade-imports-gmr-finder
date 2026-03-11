using AutoFixture;
using AutoFixture.Dsl;
using Defra.TradeImportsDataApi.Domain.Events;
using Defra.TradeImportsDataApi.Domain.Ipaffs;

namespace TestFixtures;

public static class ImportPreNotificationFixtures
{
    public static IPostprocessComposer<
        ResourceEvent<ImportPreNotificationEvent>
    > ImportPreNotificationResourceEventFixture(ImportPreNotification importPreNotification)
    {
        var importPreNotificationEvent = new ImportPreNotificationEvent
        {
            Id = "CHEDPP.GB.2025.1053368",
            ImportPreNotification = importPreNotification,
        };

        return GetFixture()
            .Build<ResourceEvent<ImportPreNotificationEvent>>()
            .With(x => x.Resource, importPreNotificationEvent)
            .With(x => x.ResourceId, "CHEDPP.GB.2025.1053368")
            .With(x => x.ResourceType, ResourceEventResourceTypes.ImportPreNotification);
    }

    public static IPostprocessComposer<ImportPreNotification> ImportPreNotificationFixture(string? mrn = null)
    {
        var importPreNotification = GetFixture().Build<ImportPreNotification>();
        if (mrn == null)
        {
            return importPreNotification;
        }

        return importPreNotification.With(
            x => x.ExternalReferences,
            [new ExternalReference { Reference = mrn, System = "NCTS" }]
        );
    }

    private static Fixture GetFixture()
    {
        var fixture = new Fixture();
        fixture.Customize<DateOnly>(o => o.FromFactory((DateTime dt) => DateOnly.FromDateTime(dt)));
        return fixture;
    }
}
