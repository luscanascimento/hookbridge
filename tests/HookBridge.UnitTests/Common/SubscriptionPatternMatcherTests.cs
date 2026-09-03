using FluentAssertions;
using HookBridge.Application.Common;

namespace HookBridge.UnitTests.Common;

public class SubscriptionPatternMatcherTests
{
    [Theory]
    [InlineData("*", "order.created", true)]
    [InlineData("*", "payment.settled.v1", true)]
    [InlineData("*", "anything", true)]
    [InlineData("order.*", "order.created", true)]
    [InlineData("order.*", "order.paid.v2", true)]
    [InlineData("order.*", "order", true)]
    [InlineData("order.*", "payment.created", false)]
    [InlineData("order.created", "order.created", true)]
    [InlineData("order.created", "order.cancelled", false)]
    [InlineData("ORDER.CREATED", "order.created", true)]
    [InlineData("", "order.created", false)]
    [InlineData(null, "order.created", false)]
    public void Matches_ShouldEvaluateCorrectly(string? pattern, string eventType, bool expected)
    {
        // Act
        var result = SubscriptionPatternMatcher.Matches(pattern!, eventType);

        // Assert
        result.Should().Be(expected);
    }
}
