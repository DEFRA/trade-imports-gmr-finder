using System;

namespace GmrFinder.Configuration;

public class PollingServiceOptions
{
    public const string SectionName = "PollingService";

    public int MaxPollSize { get; init; } = 500;
    public TimeSpan ExpiryTimeSpan { get; init; } = TimeSpan.FromDays(30);
}
