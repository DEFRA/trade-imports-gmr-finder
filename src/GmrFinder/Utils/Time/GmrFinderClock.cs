namespace GmrFinder.Utils.Time;

public sealed class GmrFinderClock : IGmrFinderClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}
