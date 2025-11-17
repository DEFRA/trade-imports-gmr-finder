namespace GmrFinder.Configuration;

public class MatchedGmrsProducerOptions
{
    public const string SectionName = "MatchedGmrsProducer";

    public required string TopicArn { get; init; }
}
