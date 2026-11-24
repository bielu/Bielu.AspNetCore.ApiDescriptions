using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Shouldly;
using Xunit;

namespace Bielu.AspNetCore.Arazzo.Scalar.Tests.Integration;

/// <summary>
/// The ArazzoScalar example's receive steps, evaluated against the messages its services send: a payment or stock
/// result must take the step's success path when it succeeded, and its failure action when it didn't.
/// </summary>
public sealed partial class ArazzoScalarExampleCriteriaTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    public static TheoryData<string, object, bool> Messages => new()
    {
        { "paymentCompleted", Payment(PaymentStatus.Succeeded), true },
        { "paymentCompleted", Payment(PaymentStatus.Declined), false },
        { "authorized", Payment(PaymentStatus.Succeeded), true },
        { "authorized", Payment(PaymentStatus.Declined), false },
        { "stockResult", new StockChecked(Guid.NewGuid(), StockStatus.Reserved, DateTimeOffset.UtcNow), true },
        { "stockResult", new StockChecked(Guid.NewGuid(), StockStatus.OutOfStock, DateTimeOffset.UtcNow), false },
    };

    [Theory]
    [MemberData(nameof(Messages))]
    public async Task ReceiveStep_SerializedMessage_TakesThePathItsStatusLeadsTo(string stepId, object message, bool succeeded)
    {
        // Arrange
        var step = await StepAsync(stepId);
        var payload = JsonSerializer.SerializeToNode(message, message.GetType(), Web)!;

        // Act
        var success = step["successCriteria"]!.AsArray().All(criterion => Holds(criterion!, payload));
        var failures = step["onFailure"]!.AsArray()
            .Where(action => action!["criteria"]!.AsArray().All(criterion => Holds(criterion!, payload)))
            .Select(action => (string)action!["name"]!)
            .ToList();

        // Assert: exactly one way on, the one the status leads to.
        success.ShouldBe(succeeded);
        failures.Count.ShouldBe(succeeded ? 0 : 1);
    }

    private static PaymentCompleted Payment(PaymentStatus status) =>
        new(Guid.NewGuid(), "pay_1", status, 42m, DateTimeOffset.UtcNow);

    private async Task<JsonNode> StepAsync(string stepId)
    {
        using var client = factory.CreateClient();
        var document = JsonNode.Parse(await client.GetStringAsync(new Uri("/arazzo/v1.json", UriKind.Relative)))!;
        return document["workflows"]!.AsArray()
            .SelectMany(workflow => workflow!["steps"]!.AsArray())
            .Single(step => (string)step!["stepId"]! == stepId)!;
    }

    /// <summary>
    /// Evaluates a simple criterion comparing a value of the message payload, through a JSON Pointer, with a string:
    /// the form the example's receive steps use. Anything else fails the test, rather than passing unevaluated.
    /// </summary>
    private static bool Holds(JsonNode criterion, JsonNode payload)
    {
        criterion["type"].ShouldBeNull("a receive step's criteria should be simple conditions");
        var condition = (string)criterion["condition"]!;
        var match = PayloadComparison().Match(condition);
        match.Success.ShouldBeTrue($"'{condition}' isn't a $message.payload#/pointer == 'value' comparison");

        JsonNode? value = payload;
        foreach (var token in match.Groups["pointer"].Value.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            value = value?[token.Replace("~1", "/", StringComparison.Ordinal).Replace("~0", "~", StringComparison.Ordinal)];
        }

        return value?.GetValueKind() == JsonValueKind.String && (string)value! == match.Groups["value"].Value;
    }

    [GeneratedRegex(@"^\$message\.payload#(?<pointer>/\S*)\s*==\s*'(?<value>[^']*)'$")]
    private static partial Regex PayloadComparison();
}
