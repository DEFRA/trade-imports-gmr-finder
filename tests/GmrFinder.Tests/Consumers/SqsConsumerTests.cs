using Amazon.SQS;
using Amazon.SQS.Model;
using GmrFinder.Consumers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace GmrFinder.Tests.Consumers;

public class SqsConsumerTests
{
    private readonly ILogger<TestConsumer> _logger = NullLogger<TestConsumer>.Instance;
    private readonly Mock<IAmazonSQS> _mockSqsClient = new();
    private const string QueueName = "queue_name";
    private const string QueueUrl = "http://queue-url";

    private readonly Message _message = new()
    {
        MessageId = Guid.NewGuid().ToString(),
        ReceiptHandle = Guid.NewGuid().ToString(),
        Body = "payload",
    };

    private TestConsumer _consumer;

    public SqsConsumerTests()
    {
        _consumer = new TestConsumer(_logger, _mockSqsClient.Object, QueueName);

        _mockSqsClient
            .Setup(s => s.GetQueueUrlAsync(QueueName, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetQueueUrlResponse { QueueUrl = QueueUrl });

        _mockSqsClient
            .Setup(s => s.ReceiveMessageAsync(QueueUrl, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ReceiveMessageResponse { Messages = [_message] });
    }

    [Fact]
    public async Task ExecuteAsync_WhenMessageReceived_ProcessesAndDeletesMessage()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        var tcs = new TaskCompletionSource<Message>(TaskCreationOptions.RunContinuationsAsynchronously);

        _mockSqsClient
            .Setup(s => s.DeleteMessageAsync(QueueUrl, _message.ReceiptHandle, It.IsAny<CancellationToken>()))
            .Callback(cts.Cancel)
            .ReturnsAsync(new DeleteMessageResponse());

        _consumer = new TestConsumer(
            _logger,
            _mockSqsClient.Object,
            QueueName,
            (msg, _) =>
            {
                tcs.TrySetResult(msg);
                return Task.CompletedTask;
            }
        );

        var runTask = _consumer.RunAsync(cts.Token);

        var processedMessage = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);
        await runTask.WaitAsync(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);

        Assert.Equal(_message.Body, processedMessage.Body);
        _mockSqsClient.Verify(s => s.ReceiveMessageAsync(QueueUrl, It.IsAny<CancellationToken>()), Times.Once);
        _mockSqsClient.Verify(
            s => s.DeleteMessageAsync(QueueUrl, _message.ReceiptHandle, It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    [Fact]
    public async Task ExecuteAsync_WhenMessageReceived_AndIsFailedToBeProcessed_ItDoesNotDeleteTheMessage()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));

        _consumer = new TestConsumer(
            _logger,
            _mockSqsClient.Object,
            QueueName,
            (msg, _) =>
            {
                throw new Exception("Failed to process message");
            }
        );

        var runTask = _consumer.RunAsync(cts.Token);

        await runTask.WaitAsync(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);

        _mockSqsClient.Verify(s => s.ReceiveMessageAsync(QueueUrl, It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        _mockSqsClient.Verify(
            s => s.DeleteMessageAsync(QueueUrl, _message.ReceiptHandle, It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    private sealed class TestConsumer(
        ILogger<TestConsumer> logger,
        IAmazonSQS sqsClient,
        string queueName,
        Func<Message, CancellationToken, Task>? onProcessMessage = null
    ) : SqsConsumer<TestConsumer>(logger, sqsClient, queueName)
    {
        private readonly Func<Message, CancellationToken, Task> _onProcessMessage =
            onProcessMessage ?? ((_, _) => Task.CompletedTask);

        protected override TimeSpan PollDelay => TimeSpan.Zero;

        protected override Task ProcessMessageAsync(Message message, CancellationToken stoppingToken)
        {
            return _onProcessMessage(message, stoppingToken);
        }

        public Task RunAsync(CancellationToken cancellationToken) => ExecuteAsync(cancellationToken);
    }
}
