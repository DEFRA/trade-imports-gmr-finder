using Defra.TradeImportsDataApi.Domain.Events;
using Defra.TradeImportsDataApi.Domain.Ipaffs;

namespace GmrFinder.Processing;

public interface IImportPreNotificationProcessor
{
    Task ProcessAsync(
        ResourceEvent<ImportPreNotificationEvent> importPreNotification,
        CancellationToken cancellationToken
    );
}
