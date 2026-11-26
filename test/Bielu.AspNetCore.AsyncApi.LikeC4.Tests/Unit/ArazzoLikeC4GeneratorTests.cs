using Bielu.AspNetCore.AsyncApi.LikeC4.Tests.Fixtures;
using Shouldly;
using Xunit;

namespace Bielu.AspNetCore.AsyncApi.LikeC4.Tests.Unit;

public class ArazzoLikeC4GeneratorTests
{
    private const string Orders = """
        {
          "asyncapi": "3.0.0",
          "info": { "title": "Orders", "version": "1" },
          "channels": { "created": { "address": "orders.created", "messages": { "m": { "name": "OrderCreated" } } } },
          "operations": { "publish": { "action": "send", "channel": { "$ref": "#/channels/created" } } }
        }
        """;

    private const string OrdersSource = """[ { "name": "orders", "url": "https://orders.example.com/asyncapi.json", "type": "asyncapi" } ]""";

    private static ArazzoLikeC4Source Workflow(string name, string sources, string steps, string extra = "", string workflowId = "run") =>
        new(name, $$"""
            {
              "arazzo": "1.1.0",
              "info": { "title": "{{name}}", "version": "1" },
              {{extra}}
              "sourceDescriptions": {{sources}},
              "workflows": [ { "workflowId": "{{workflowId}}", "steps": {{steps}} } ]
            }
            """);

    [Fact]
    public void Generate_ShopDocumentsAndCheckoutWorkflows_MatchesGoldenModel()
    {
        // Arrange
        var workflows = new[] { new ArazzoLikeC4Source("checkout", ShopFixtures.Read("checkout.arazzo.yaml")) };

        // Act
        var model = AsyncApiLikeC4Generator.Generate(ShopFixtures.Sources(), workflows);

        // Assert
        ShopFixtures.ShouldMatchGolden(model, "shop-checkout.c4");
    }

    [Fact]
    public void Generate_WorkflowsAlone_DrawEverySourceAsAnElementOfItsOwn()
    {
        // Arrange
        var workflow = Workflow("checkout", OrdersSource, """[ { "stepId": "s", "operationId": "publish" } ]""");

        // Act
        var model = AsyncApiLikeC4Generator.Generate([], [workflow]);

        // Assert
        model.ShouldStartWith("// Generated from Arazzo documents");
        model.ShouldNotContain("asyncapiService");
        model.ShouldContain("orders = arazzoSource 'orders' {");
        model.ShouldContain("technology 'AsyncAPI'");
        model.ShouldContain("client = arazzoActor 'Client' {");
        model.ShouldContain("Runs the workflows.");
        model.ShouldContain("client -> orders 'publish'");
    }

    [Fact]
    public void Generate_SourceMatchedByLink_IsTheAsyncApiService()
    {
        // Arrange
        var service = new AsyncApiLikeC4Source("shop-orders", Orders) { Link = "https://orders.example.com/asyncapi.json" };
        var workflow = Workflow("checkout", OrdersSource, """[ { "stepId": "s", "operationId": "$sourceDescriptions.orders.publish" } ]""");

        // Act
        var model = AsyncApiLikeC4Generator.Generate([service], [workflow]);

        // Assert
        model.ShouldNotContain("arazzoSource '");
        model.ShouldContain("ordersCreated -> client 'delivers OrderCreated'");
    }

    [Fact]
    public void Generate_StepAction_OverridesTheSideTheServiceImplies()
    {
        // Arrange
        var workflow = Workflow("checkout", OrdersSource, """[ { "stepId": "s", "operationId": "publish", "action": "send" } ]""");

        // Act
        var model = AsyncApiLikeC4Generator.Generate([new AsyncApiLikeC4Source("orders", Orders)], [workflow]);

        // Assert
        model.ShouldContain("client -> ordersCreated 'sends OrderCreated'");
    }

    [Fact]
    public void Generate_StepOnUnknownOperationOrChannel_CallsTheService()
    {
        // Arrange
        var workflow = Workflow("checkout", OrdersSource, """
            [
              { "stepId": "a", "operationId": "$sourceDescriptions.orders.missing" },
              { "stepId": "b", "channelPath": "{$sourceDescriptions.orders.url}#/channels/gone" },
              { "stepId": "c", "channelPath": "{$sourceDescriptions.orders.url}#/channels/created" }
            ]
            """);

        // Act
        var model = AsyncApiLikeC4Generator.Generate([new AsyncApiLikeC4Source("orders", Orders)], [workflow]);

        // Assert
        model.ShouldContain("    client -> orders 'missing'\n    client -> orders 'gone'\n    client -> ordersCreated 'sends OrderCreated'\n");
    }

    [Fact]
    public void Generate_StepsNamingNoKnownSource_AreLeftOut()
    {
        // Arrange
        var workflow = Workflow("checkout", "[]", """
            [
              { "stepId": "a", "operationId": "orphan" },
              { "stepId": "b", "operationPath": "https://elsewhere.example.com/openapi.json#/paths/~1x/get" },
              { "stepId": "c", "workflowId": "$sourceDescriptions.nowhere.run" }
            ]
            """);

        // Act
        var model = AsyncApiLikeC4Generator.Generate([], [workflow]);

        // Assert
        model.ShouldContain("  dynamic view arazzoRun {\n    title 'run'\n    variant sequence\n  }\n");
    }

    [Fact]
    public void Generate_WorkflowOfAnotherDocument_IsOpenedFromTheStepInvokingIt()
    {
        // Arrange
        var checkout = Workflow("checkout", """
            [
              { "name": "billing", "url": "https://example.com/billing.arazzo.json", "type": "arazzo" },
              { "name": "payments", "url": "https://example.com/other.arazzo.json", "type": "arazzo" },
              { "name": "refunding", "url": "https://example.com/refunds.arazzo.json", "type": "arazzo" }
            ]
            """, """
            [
              { "stepId": "a", "workflowId": "$sourceDescriptions.payments.pay" },
              { "stepId": "b", "workflowId": "$sourceDescriptions.refunding.refund" },
              { "stepId": "c", "workflowId": "$sourceDescriptions.billing.settle" }
            ]
            """);
        var payments = Workflow("payments", "[]", "[]", workflowId: "pay");
        var refunds = Workflow("refunds", "[]", "[]", "\"$self\": \"https://example.com/refunds.arazzo.json\",", "refund");

        // Act
        var model = AsyncApiLikeC4Generator.Generate([], [checkout, payments, refunds]);

        // Assert
        model.ShouldContain("client -> client 'runs pay' {\n      navigateTo arazzoPay\n    }");
        model.ShouldContain("client -> client 'runs refund' {\n      navigateTo arazzoRefund\n    }");
        model.ShouldContain("client -> billing 'runs settle'");
        model.ShouldContain("technology 'Arazzo'");
    }

    [Fact]
    public void Generate_WorkflowFoundByLink_IsOpenedFromTheStepInvokingIt()
    {
        // Arrange
        var checkout = Workflow(
            "checkout",
            """[ { "name": "pay", "url": "https://example.com/pay.arazzo.json", "type": "arazzo" } ]""",
            """[ { "stepId": "a", "workflowId": "$sourceDescriptions.pay.charge" } ]""");
        var payments = Workflow("payments", "[]", "[]", workflowId: "charge") is var source
            ? new ArazzoLikeC4Source(source.Name, source.Document) { Link = "https://example.com/pay.arazzo.json" }
            : null!;

        // Act
        var model = AsyncApiLikeC4Generator.Generate([], [checkout, payments]);

        // Assert
        model.ShouldContain("navigateTo arazzoCharge");
    }

    [Fact]
    public void Generate_SameSourceUrlInTwoDocuments_IsOneElement()
    {
        // Arrange
        var sources = """[ { "name": "payments", "url": "https://payments.example.com/openapi.json" } ]""";
        var steps = """[ { "stepId": "a", "operationId": "pay" } ]""";

        // Act
        var model = AsyncApiLikeC4Generator.Generate([], [Workflow("one", sources, steps), Workflow("two", sources, steps)]);

        // Assert
        model.Split("= arazzoSource").Length.ShouldBe(2);
        model.ShouldContain("technology 'OpenAPI'");
        model.ShouldContain("dynamic view arazzoRun2 {");
    }

    [Fact]
    public void Generate_SourceUrlWithWhitespace_HasNoLink()
    {
        // Arrange
        var workflow = Workflow("one", """[ { "name": "p", "url": "docs/pay ments.json", "type": "custom" } ]""", """[ { "stepId": "a", "operationId": "x" } ]""");

        // Act
        var model = AsyncApiLikeC4Generator.Generate([], [workflow]);

        // Assert
        model.ShouldNotContain("link ");
        model.ShouldContain("technology 'custom'");
    }

    [Fact]
    public void Generate_ActorRegistryWithoutClient_KeepsTheDefaultClientAndTitlesTheOthers()
    {
        // Arrange
        var workflow = Workflow(
            "one",
            OrdersSource,
            """[ { "stepId": "a", "operationId": "x", "x-actor": "ops" }, { "stepId": "b", "operationId": "y" } ]""",
            "\"x-actors\": { \"ops\": { \"title\": \"Operations\", \"description\": \"Keeps it running.\" }, \"broken\": 3 },");

        // Act
        var model = AsyncApiLikeC4Generator.Generate([], [workflow]);

        // Assert
        model.ShouldContain("ops = arazzoActor 'Operations' {");
        model.ShouldContain("Keeps it running.");
        model.ShouldContain("ops -> orders 'x'");
        model.ShouldContain("client -> orders 'y'");
    }

    [Fact]
    public void Generate_ThrowsForNullWorkflows()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() => AsyncApiLikeC4Generator.Generate([], null!));
    }

    [Fact]
    public void Generate_ThrowsForNoDocumentsAtAll()
    {
        // Act & Assert
        Should.Throw<ArgumentException>(() => AsyncApiLikeC4Generator.Generate([], []))
            .Message.ShouldContain("At least one AsyncAPI or Arazzo document");
    }

    [Fact]
    public void Generate_ThrowsForDuplicateWorkflowDocumentNames()
    {
        // Arrange
        var workflow = Workflow("checkout", "[]", "[]");

        // Act & Assert
        Should.Throw<ArgumentException>(() => AsyncApiLikeC4Generator.Generate([], [workflow, workflow]))
            .Message.ShouldContain("Two Arazzo documents are named 'checkout'");
    }

    [Theory]
    [InlineData("{ not json")]
    [InlineData("{ \"arazzo\": \"1.1.0\" }")]
    public void Generate_ThrowsForContentThatIsNotAnArazzoDocument(string content)
    {
        // Act & Assert
        Should.Throw<FormatException>(() => AsyncApiLikeC4Generator.Generate([], [new ArazzoLikeC4Source("broken", content)]))
            .Message.ShouldStartWith("Arazzo document 'broken':");
    }

    [Theory]
    [InlineData(null)]
    [InlineData(" ")]
    public void Source_ThrowsForMissingName(string? name)
    {
        // Act & Assert
        Should.Throw<ArgumentException>(() => new ArazzoLikeC4Source(name!, "{}"));
    }

    [Fact]
    public void Source_ThrowsForNullDocument()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() => new ArazzoLikeC4Source("checkout", null!));
    }
}
