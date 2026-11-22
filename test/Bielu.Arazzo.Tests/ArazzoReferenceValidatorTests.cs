using System.Text.Json.Nodes;
using Bielu.Arazzo.Models;
using Bielu.Arazzo.SourceResolvers;
using Bielu.Arazzo.Validation;
using Shouldly;
using Xunit;

namespace Bielu.Arazzo.Tests;

public class ArazzoReferenceValidatorTests
{
    private static readonly JsonNode OpenApi = JsonNode.Parse("""
        {
            "openapi": "3.1.0",
            "paths": { "/pets/{id}": { "get": { "operationId": "getPet" } } }
        }
        """)!;

    private static readonly JsonNode AsyncApi = JsonNode.Parse("""
        {
            "asyncapi": "3.0.0",
            "channels": { "orders": { "address": "orders" } },
            "operations": { "publishOrder": { "action": "send" } }
        }
        """)!;

    private static ArazzoWorkspace Workspace(bool withOpenApi = true, bool withAsyncApi = true)
    {
        var workspace = new ArazzoWorkspace();
        workspace.RegisterResolver(new OpenApiJsonSourceResolver());
        workspace.RegisterResolver(new AsyncApiJsonSourceResolver());
        if (withOpenApi)
        {
            workspace.RegisterDocument("pets", "openapi", OpenApi);
        }

        if (withAsyncApi)
        {
            workspace.RegisterDocument("events", "asyncapi", AsyncApi);
        }

        return workspace;
    }

    private static ArazzoDocument Document(params ArazzoStep[] steps) => new()
    {
        Arazzo = "1.1.0",
        Info = new ArazzoInfo { Title = "t", Version = "1" },
        SourceDescriptions =
        [
            new ArazzoSourceDescription { Name = "pets", Url = "pets.json", Type = ArazzoSourceDescriptionType.OpenApi },
            new ArazzoSourceDescription { Name = "events", Url = "events.json", Type = ArazzoSourceDescriptionType.AsyncApi },
        ],
        Workflows = [new ArazzoWorkflow { WorkflowId = "flow", Steps = [.. steps] }],
    };

    [Fact]
    public void Validate_EveryReferenceResolves_ReturnsNoErrors()
    {
        // Arrange
        var document = Document(
            new ArazzoStep { StepId = "bare", OperationId = "getPet" },
            new ArazzoStep { StepId = "qualified", OperationId = "$sourceDescriptions.events.publishOrder" },
            new ArazzoStep { StepId = "path", OperationPath = "{$sourceDescriptions.pets.url}#/paths/~1pets~1{id}/get" },
            new ArazzoStep { StepId = "channel", ChannelPath = "{$sourceDescriptions.events.url}#/channels/orders" });

        // Act
        var errors = ArazzoReferenceValidator.Validate(document, Workspace());

        // Assert
        errors.ShouldBeEmpty();
    }

    [Fact]
    public void Validate_RenamedOperation_ReportsTheStep()
    {
        // Arrange
        var document = Document(new ArazzoStep { StepId = "get", OperationId = "$sourceDescriptions.pets.fetchPet" });

        // Act
        var error = ArazzoReferenceValidator.Validate(document, Workspace()).ShouldHaveSingleItem();

        // Assert
        error.Path.ShouldBe("/workflows/0/steps/0/operationId");
        error.Message.ShouldBe("flow.get: operationId '$sourceDescriptions.pets.fetchPet' did not resolve.");
        error.IsWarning.ShouldBeFalse();
    }

    [Fact]
    public void Validate_BareOperationIdInNoSource_ReportsIt()
    {
        // Arrange
        var document = Document(new ArazzoStep { StepId = "get", OperationId = "missing" });

        // Act
        var error = ArazzoReferenceValidator.Validate(document, Workspace()).ShouldHaveSingleItem();

        // Assert
        error.Message.ShouldContain("did not resolve against any registered source");
    }

    [Fact]
    public void Validate_BareOperationIdWithNoRegisteredSource_IsSkipped()
    {
        // Arrange
        var document = Document(new ArazzoStep { StepId = "get", OperationId = "missing" });

        // Act
        var errors = ArazzoReferenceValidator.Validate(document, Workspace(withOpenApi: false, withAsyncApi: false));

        // Assert
        errors.ShouldBeEmpty();
    }

    [Fact]
    public void Validate_RemovedChannel_ReportsTheChannelPath()
    {
        // Arrange
        var document = Document(new ArazzoStep { StepId = "send", ChannelPath = "{$sourceDescriptions.events.url}#/channels/payments" });

        // Act
        var error = ArazzoReferenceValidator.Validate(document, Workspace()).ShouldHaveSingleItem();

        // Assert
        error.Path.ShouldBe("/workflows/0/steps/0/channelPath");
        error.Message.ShouldContain("did not resolve");
    }

    [Fact]
    public void Validate_MissingOperationPath_ReportsIt()
    {
        // Arrange
        var document = Document(new ArazzoStep { StepId = "delete", OperationPath = "{$sourceDescriptions.pets.url}#/paths/~1pets~1{id}/delete" });

        // Act
        var error = ArazzoReferenceValidator.Validate(document, Workspace()).ShouldHaveSingleItem();

        // Assert
        error.Path.ShouldBe("/workflows/0/steps/0/operationPath");
    }

    [Fact]
    public void Validate_OperationPathWithoutSourceExpression_ReportsIt()
    {
        // Arrange
        var document = Document(new ArazzoStep { StepId = "get", OperationPath = "/paths/~1pets/get" });

        // Act
        var error = ArazzoReferenceValidator.Validate(document, Workspace()).ShouldHaveSingleItem();

        // Assert
        error.Message.ShouldContain("did not resolve");
    }

    [Fact]
    public void Validate_UndeclaredSource_ReportsIt()
    {
        // Arrange
        var document = Document(new ArazzoStep { StepId = "send", ChannelPath = "{$sourceDescriptions.billing.url}#/channels/x" });

        // Act
        var error = ArazzoReferenceValidator.Validate(document, Workspace()).ShouldHaveSingleItem();

        // Assert
        error.Message.ShouldContain("names source 'billing', which the document doesn't declare");
    }

    [Fact]
    public void Validate_DeclaredSourceWithoutDocument_IsSkipped()
    {
        // Arrange
        var document = Document(new ArazzoStep { StepId = "get", OperationId = "$sourceDescriptions.pets.anything" });

        // Act
        var errors = ArazzoReferenceValidator.Validate(document, Workspace(withOpenApi: false));

        // Assert
        errors.ShouldBeEmpty();
    }

    [Fact]
    public void UncheckedSources_ListsDeclaredSourcesWithoutDocument()
    {
        // Arrange
        var document = Document();

        // Act
        var uncheckedSources = ArazzoReferenceValidator.UncheckedSources(document, Workspace(withAsyncApi: false));

        // Assert
        uncheckedSources.ShouldBe(["events"]);
    }

    [Fact]
    public void Validate_ThrowsForNullArguments()
    {
        Should.Throw<ArgumentNullException>(() => ArazzoReferenceValidator.Validate(null!, Workspace()));
        Should.Throw<ArgumentNullException>(() => ArazzoReferenceValidator.Validate(Document(), null!));
        Should.Throw<ArgumentNullException>(() => ArazzoReferenceValidator.UncheckedSources(null!, Workspace()));
        Should.Throw<ArgumentNullException>(() => ArazzoReferenceValidator.UncheckedSources(Document(), null!));
        Should.Throw<ArgumentNullException>(() => ArazzoReferenceValidator.ResolveStepTargets(null!, Workspace()));
        Should.Throw<ArgumentNullException>(() => ArazzoReferenceValidator.ResolveStepTargets(Document(), null!));
    }

    [Fact]
    public void ResolveStepTargets_ReportsWhatEachStepResolvedTo()
    {
        // Arrange
        var document = Document(
            new ArazzoStep { StepId = "bare", OperationId = "publishOrder" },
            new ArazzoStep { StepId = "channel", ChannelPath = "{$sourceDescriptions.events.url}#/channels/orders" },
            new ArazzoStep { StepId = "missing", OperationId = "$sourceDescriptions.pets.nope" },
            new ArazzoStep { StepId = "nested", WorkflowId = "other" });

        // Act
        var targets = ArazzoReferenceValidator.ResolveStepTargets(document, Workspace());

        // Assert
        targets.Count.ShouldBe(3);
        targets[0].ShouldSatisfyAllConditions(
            t => t.Status.ShouldBe(ArazzoStepTargetStatus.Resolved),
            t => t.SourceName.ShouldBe("events"),
            t => t.Target!["action"]!.GetValue<string>().ShouldBe("send"),
            t => t.Path.ShouldBe("/workflows/0/steps/0/operationId"));
        targets[1].ShouldSatisfyAllConditions(
            t => t.Status.ShouldBe(ArazzoStepTargetStatus.Resolved),
            t => t.Target!["address"]!.GetValue<string>().ShouldBe("orders"),
            t => t.Reference.ShouldBe("{$sourceDescriptions.events.url}#/channels/orders"));
        targets[2].ShouldSatisfyAllConditions(
            t => t.Status.ShouldBe(ArazzoStepTargetStatus.Unresolved),
            t => t.SourceName.ShouldBe("pets"),
            t => t.Target.ShouldBeNull(),
            t => t.Message.ShouldBe("operationId '$sourceDescriptions.pets.nope' did not resolve."));
    }

    [Fact]
    public void ResolveStepTargets_SourceWithoutDocument_IsSkipped()
    {
        // Arrange
        var document = Document(
            new ArazzoStep { StepId = "qualified", OperationId = "$sourceDescriptions.pets.getPet" },
            new ArazzoStep { StepId = "bare", OperationId = "getPet" });

        // Act
        var targets = ArazzoReferenceValidator.ResolveStepTargets(document, Workspace(withOpenApi: false, withAsyncApi: false));

        // Assert
        targets.ShouldAllBe(t => t.Status == ArazzoStepTargetStatus.Skipped && t.Message == null);
        targets[0].SourceName.ShouldBe("pets");
        targets[1].SourceName.ShouldBeNull();
    }
}
