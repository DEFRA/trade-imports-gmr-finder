using Defra.TradeImportsDataApi.Domain.Events;

namespace GmrFinder.Processing;

public interface ICustomsDeclarationProcessor
{
    Task ProcessAsync(ResourceEvent<CustomsDeclarationEvent> customsDeclaration, CancellationToken cancellationToken);
}
