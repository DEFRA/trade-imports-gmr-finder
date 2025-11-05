using Defra.TradeImportsDataApi.Domain.CustomsDeclaration;
using Defra.TradeImportsDataApi.Domain.Events;

namespace GmrFinder.Processing;

public interface ICustomsDeclarationProcessor
{
    Task ProcessAsync(ResourceEvent<CustomsDeclaration> customsDeclaration, CancellationToken cancellationToken);
}
