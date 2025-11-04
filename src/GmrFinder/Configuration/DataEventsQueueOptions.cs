namespace GmrFinder.Configuration;

public class DataEventsQueueConsumerOptions
{
    public const string SectionName = "DataEventsQueueConsumer";

    public required string QueueName { get; init; }
}
