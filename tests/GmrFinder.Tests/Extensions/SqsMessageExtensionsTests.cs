using System.Collections.Generic;
using Amazon.SQS.Model;
using Defra.TradeImportsDataApi.Domain.Events;
using FluentAssertions;
using GmrFinder.Extensions;
using Xunit;

namespace GmrFinder.Tests.Extensions;

public class SqsMessageExtensionsTests
{
    [Fact]
    public void GetContentEncoding_ReturnsAttributeValue_WhenHeaderExists()
    {
        var message = CreateMessage(SqsMessageHeaders.ContentEncoding, "gzip, base64");

        var result = message.GetContentEncoding();

        result.Should().Be("gzip, base64");
    }

    [Fact]
    public void GetContentEncoding_ReturnsNull_WhenHeaderMissing()
    {
        var message = CreateEmptyMessage();

        var result = message.GetContentEncoding();

        result.Should().BeNull();
    }

    [Fact]
    public void GetResourceType_ReturnsAttributeValue_WhenHeaderExists()
    {
        var message = CreateMessage(SqsMessageHeaders.ResourceType, ResourceEventResourceTypes.CustomsDeclaration);

        var result = message.GetResourceType();

        result.Should().Be(ResourceEventResourceTypes.CustomsDeclaration);
    }

    [Fact]
    public void GetResourceType_ReturnsNull_WhenHeaderMissing()
    {
        var message = CreateEmptyMessage();

        var result = message.GetResourceType();

        result.Should().BeNull();
    }

    [Fact]
    public void GetSubResourceType_ReturnsAttributeValue_WhenHeaderExists()
    {
        var message = CreateMessage(SqsMessageHeaders.SubResourceType, ResourceEventSubResourceTypes.ClearanceDecision);

        var result = message.GetSubResourceType();

        result.Should().Be(ResourceEventSubResourceTypes.ClearanceDecision);
    }

    [Fact]
    public void GetSubResourceType_ReturnsNull_WhenHeaderMissing()
    {
        var message = CreateEmptyMessage();

        var result = message.GetSubResourceType();

        result.Should().BeNull();
    }

    [Fact]
    public void GetResourceId_ReturnsAttributeValue_WhenHeaderExists()
    {
        var message = CreateMessage(SqsMessageHeaders.ResourceId, "mrn123");

        var result = message.GetResourceId();

        result.Should().Be("mrn123");
    }

    [Fact]
    public void GetResourceId_ReturnsNull_WhenHeaderMissing()
    {
        var message = CreateEmptyMessage();

        var result = message.GetResourceId();

        result.Should().BeNull();
    }

    private static Message CreateMessage(string header, string value) =>
        new()
        {
            MessageAttributes = new Dictionary<string, MessageAttributeValue>
            {
                [header] = new MessageAttributeValue { DataType = "String", StringValue = value },
            },
        };

    private static Message CreateEmptyMessage() => new() { MessageAttributes = [] };
}
