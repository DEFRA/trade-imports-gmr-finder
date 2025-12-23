using System.Diagnostics.Metrics;
using Moq;

namespace GmrFinder.Tests.Metrics;

internal class MockMeterFactory
{
    private readonly Mock<IMeterFactory> _meterFactory = new();

    internal MockMeterFactory()
    {
        _meterFactory.Setup(x => x.Create(It.IsAny<MeterOptions>())).Returns(new Meter("test"));
    }

    public IMeterFactory CreateMeter()
    {
        return _meterFactory.Object;
    }
}
