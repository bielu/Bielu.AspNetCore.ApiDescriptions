using System.Text.Json.Nodes;
using Bielu.Arazzo.Models;
using Bielu.Arazzo.Writers;
using Bielu.AspNetCore.Arazzo.Extensions;
using Bielu.AspNetCore.Arazzo.Services;
using Bielu.AspNetCore.Arazzo.Transformers;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Bielu.AspNetCore.Arazzo.Tests.Unit;

public class ArazzoDocumentTransformerTests
{
    private static IArazzoDocumentProvider BuildProvider(Action<ArazzoOptions> configure, Action<IServiceCollection>? services = null)
    {
        var collection = new ServiceCollection();
        services?.Invoke(collection);
        collection.AddArazzo(options =>
        {
            options.ValidateSourceReferencesOnStartup = false;
            options.WithInfo("Test", "1.0.0");
            options.AddSourceDescription("api", "https://example.com/openapi.json", ArazzoSourceDescriptionType.OpenApi);
            options.AddWorkflow("wf", wf => wf.Step("step", s => s.Operation("createOrder")));
            configure(options);
        });
        return collection.BuildServiceProvider().GetRequiredKeyedService<IArazzoDocumentProvider>(ArazzoDefaults.DefaultDocumentName);
    }

    [Fact]
    public async Task AddDocumentTransformer_RunsTransformersInRegistrationOrder()
    {
        // Arrange
        var calls = new List<string>();
        var provider = BuildProvider(options => options
            .AddDocumentTransformer((document, context, _) =>
            {
                calls.Add($"delegate:{context.DocumentName}");
                document.Info.Summary = "first";
                return Task.CompletedTask;
            })
            .AddDocumentTransformer(new SummaryAppender(" second")));

        // Act
        var document = await provider.GetArazzoDocumentAsync();

        // Assert
        calls.ShouldBe([$"delegate:{ArazzoDefaults.DefaultDocumentName}"]);
        document.Info.Summary.ShouldBe("first second");
    }

    [Fact]
    public async Task AddDocumentTransformer_ByType_ResolvesDependenciesAndDisposesTheInstance()
    {
        // Arrange
        var tracker = new DisposalTracker();
        var provider = BuildProvider(
            options => options.AddDocumentTransformer<WorkflowAddingTransformer>(),
            services => services.AddSingleton(tracker));

        // Act
        var document = await provider.GetArazzoDocumentAsync();

        // Assert
        document.Workflows.Select(w => w.WorkflowId).ShouldBe(["wf", "added"]);
        tracker.Disposed.ShouldBe(1);
    }

    [Fact]
    public async Task Transformers_RunOnceAndTheResultIsCached()
    {
        // Arrange
        var runs = 0;
        var provider = BuildProvider(options => options.AddDocumentTransformer((_, _, _) =>
        {
            Interlocked.Increment(ref runs);
            return Task.CompletedTask;
        }));

        // Act
        var documents = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => provider.GetArazzoDocumentAsync()));

        // Assert
        runs.ShouldBe(1);
        documents.Distinct().ShouldHaveSingleItem();
    }

    [Fact]
    public async Task Transformer_ThatMakesTheDocumentInvalid_FailsValidation()
    {
        // Arrange
        var provider = BuildProvider(options => options.AddDocumentTransformer((document, _, _) =>
        {
            document.Workflows[0].Steps[0].Outputs = new Dictionary<string, ArazzoValue>
            {
                ["count"] = ArazzoValue.FromLiteral(3),
            };
            return Task.CompletedTask;
        }));

        // Act
        var exception = await Should.ThrowAsync<ArazzoDocumentValidationException>(() => provider.GetArazzoDocumentAsync());

        // Assert
        exception.Errors.ShouldContain(e => e.Path == "/workflows/0/steps/0/outputs/count");
    }

    [Fact]
    public async Task GetArazzoDocumentAsync_RetryAfterTransformerMutatedThenThrew_AppendsTheStepOnce()
    {
        // Arrange
        var attempts = 0;
        var provider = BuildProvider(options => options.AddDocumentTransformer((document, _, _) =>
        {
            document.Workflows[0].Steps.Add(new ArazzoStep { StepId = "appended", OperationId = "$sourceDescriptions.api.getOrder" });
            return ++attempts == 1 ? throw new InvalidOperationException("transient") : Task.CompletedTask;
        }));
        await Should.ThrowAsync<InvalidOperationException>(() => provider.GetArazzoDocumentAsync());

        // Act
        var document = await provider.GetArazzoDocumentAsync();

        // Assert
        attempts.ShouldBe(2);
        document.Workflows[0].Steps.Select(s => s.StepId).ShouldBe(["step", "appended"]);
    }

    [Fact]
    public async Task GetArazzoDocumentAsync_RetryAfterCancellationFollowingMutation_AppendsTheStepOnce()
    {
        // Arrange
        using var cancellation = new CancellationTokenSource();
        var provider = BuildProvider(options => options.AddDocumentTransformer(async (document, _, cancellationToken) =>
        {
            document.Workflows[0].Steps.Add(new ArazzoStep { StepId = "appended", OperationId = "$sourceDescriptions.api.getOrder" });
            if (!cancellation.IsCancellationRequested && cancellationToken == cancellation.Token)
            {
                await cancellation.CancelAsync();
            }

            cancellationToken.ThrowIfCancellationRequested();
        }));
        await Should.ThrowAsync<OperationCanceledException>(() => provider.GetArazzoDocumentAsync(cancellation.Token));

        // Act
        var document = await provider.GetArazzoDocumentAsync();

        // Assert
        document.Workflows[0].Steps.Select(s => s.StepId).ShouldBe(["step", "appended"]);
    }

    [Fact]
    public async Task GetArazzoDocumentAsync_RetryAfterFailedAttempt_StartsFromTheConfiguredModel()
    {
        // Arrange
        var failed = false;
        var provider = BuildProvider(options => options
            .WithExtension("x-origin", "configured")
            .AddComponentParameter("tenant", new ArazzoParameter { Name = "tenant", In = ArazzoParameterLocation.Header, Value = "$inputs.tenant" })
            .AddDocumentTransformer((document, _, _) =>
            {
                if (failed)
                {
                    return Task.CompletedTask;
                }

                failed = true;
                document.Info.Title = "mutated";
                document.SourceDescriptions[0].Url = "https://example.com/mutated.json";
                document.SourceDescriptions.Clear();
                document.Extensions!["x-origin"] = "mutated";
                document.Components!.Parameters!["tenant"].Name = "mutated";
                document.Workflows[0].Steps[0].Outputs = new Dictionary<string, ArazzoValue> { ["id"] = "$response.body#/id" };
                throw new InvalidOperationException("transient");
            }));
        await Should.ThrowAsync<InvalidOperationException>(() => provider.GetArazzoDocumentAsync());

        // Act
        var document = await provider.GetArazzoDocumentAsync();

        // Assert
        document.Info.Title.ShouldBe("Test");
        document.SourceDescriptions.ShouldHaveSingleItem().Url.ShouldBe("https://example.com/openapi.json");
        document.Extensions!["x-origin"]!.GetValue<string>().ShouldBe("configured");
        document.Components!.Parameters!["tenant"].Name.ShouldBe("tenant");
        document.Workflows[0].Steps[0].Outputs.ShouldBeNull();
    }

    [Fact]
    public void ArazzoModelCopy_CopiesEveryConfiguredValueIntoNewInstances()
    {
        // Arrange
        var selector = new ArazzoSelector { Context = "$response.body", Selector = "$.id", Type = new ArazzoSelectorType { Type = "jsonpath", Version = "rfc9535" }, Extensions = new Dictionary<string, JsonNode?> { ["x-s"] = 1 } };
        var criterion = new ArazzoCriterion { Context = "$response.body", Condition = "$.ok", Type = new ArazzoSelectorType { Type = "jsonpath" }, Extensions = new Dictionary<string, JsonNode?> { ["x-c"] = 1 } };
        var parameter = new ArazzoParameter { Name = "id", In = ArazzoParameterLocation.Path, Value = ArazzoValue.FromSelector(selector), Extensions = new Dictionary<string, JsonNode?> { ["x-p"] = 1 } };
        var success = new ArazzoSuccessAction { Name = "next", Type = ArazzoSuccessActionType.Goto, StepId = "s2", Criteria = [criterion], Extensions = new Dictionary<string, JsonNode?> { ["x-sa"] = 1 } };
        var failure = new ArazzoFailureAction
        {
            Name = "retry",
            Type = ArazzoFailureActionType.Retry,
            WorkflowId = "other",
            Parameters = [ArazzoReferenceable<ArazzoParameter>.Of(new ArazzoParameter { Name = "p", Value = "$inputs.p" })],
            RetryAfter = 1.5,
            RetryLimit = 3,
            Criteria = [criterion],
            Extensions = new Dictionary<string, JsonNode?> { ["x-fa"] = 1 },
        };
        var workflow = new ArazzoWorkflow
        {
            WorkflowId = "wf",
            Summary = "summary",
            Description = "description",
            Inputs = JsonNode.Parse("""{"type":"object"}"""),
            DependsOn = ["other"],
            Steps =
            [
                new ArazzoStep
                {
                    StepId = "s1",
                    Description = "first",
                    OperationPath = "{$sourceDescriptions.api.url}#/paths/~1orders/post",
                    Parameters = [ArazzoReferenceable<ArazzoParameter>.Of(parameter), ArazzoReferenceable<ArazzoParameter>.Of(new ArazzoReusableObject { Reference = "$components.parameters.tenant", Value = "acme" })],
                    RequestBody = new ArazzoRequestBody
                    {
                        ContentType = "application/json",
                        Payload = ArazzoValue.FromLiteral(JsonNode.Parse("""{"a":1}""")),
                        Replacements = [new ArazzoPayloadReplacement { Target = "$.a", TargetSelectorType = new ArazzoSelectorType { Type = "jsonpath" }, Value = "$inputs.a", Extensions = new Dictionary<string, JsonNode?> { ["x-r"] = 1 } }],
                        Extensions = new Dictionary<string, JsonNode?> { ["x-b"] = 1 },
                    },
                    SuccessCriteria = [criterion],
                    OnSuccess = [ArazzoReferenceable<ArazzoSuccessAction>.Of(success)],
                    OnFailure = [ArazzoReferenceable<ArazzoFailureAction>.Of(failure), ArazzoReferenceable<ArazzoFailureAction>.Of(new ArazzoReusableObject { Reference = "$components.failureActions.stop" })],
                    Outputs = new Dictionary<string, ArazzoValue> { ["id"] = "$response.body#/id" },
                    Extensions = new Dictionary<string, JsonNode?> { ["x-st"] = 1 },
                },
                new ArazzoStep
                {
                    StepId = "s2",
                    ChannelPath = "{$sourceDescriptions.events.url}#/channels/orders",
                    Action = ArazzoStepAction.Receive,
                    CorrelationId = "orderId",
                    Timeout = 1000,
                    DependsOn = ["s1"],
                },
                new ArazzoStep { StepId = "s3", OperationId = "$sourceDescriptions.api.getOrder" },
                new ArazzoStep { StepId = "s4", WorkflowId = "other" },
            ],
            SuccessActions = [ArazzoReferenceable<ArazzoSuccessAction>.Of(success)],
            FailureActions = [ArazzoReferenceable<ArazzoFailureAction>.Of(failure)],
            Outputs = new Dictionary<string, ArazzoValue> { ["id"] = ArazzoValue.FromSelector(selector) },
            Parameters = [ArazzoReferenceable<ArazzoParameter>.Of(parameter)],
            Extensions = new Dictionary<string, JsonNode?> { ["x-w"] = 1 },
        };
        var components = new ArazzoComponents
        {
            Inputs = new Dictionary<string, JsonNode?> { ["in"] = JsonNode.Parse("""{"type":"string"}""") },
            Parameters = new Dictionary<string, ArazzoParameter> { ["tenant"] = parameter },
            SuccessActions = new Dictionary<string, ArazzoSuccessAction> { ["next"] = success },
            FailureActions = new Dictionary<string, ArazzoFailureAction> { ["stop"] = failure },
            Extensions = new Dictionary<string, JsonNode?> { ["x-co"] = 1 },
        };
        var original = new ArazzoDocument
        {
            Arazzo = "1.1.0",
            Info = new ArazzoInfo { Title = "t", Summary = "s", Description = "d", Version = "1.0.0", Extensions = new Dictionary<string, JsonNode?> { ["x-i"] = 1 } },
            SourceDescriptions = [new ArazzoSourceDescription { Name = "api", Url = "https://example.com/openapi.json", Type = ArazzoSourceDescriptionType.OpenApi, Extensions = new Dictionary<string, JsonNode?> { ["x-sd"] = 1 } }],
            Workflows = [workflow],
            Components = components,
            Extensions = new Dictionary<string, JsonNode?> { ["x-d"] = JsonNode.Parse("""{"nested":[1]}""") },
        };

        // Act
        var copy = new ArazzoDocument
        {
            Arazzo = original.Arazzo,
            Info = ArazzoModelCopy.Copy(original.Info),
            SourceDescriptions = [.. original.SourceDescriptions.Select(ArazzoModelCopy.Copy)],
            Workflows = [.. original.Workflows.Select(ArazzoModelCopy.Copy)],
            Components = ArazzoModelCopy.Copy(original.Components),
            Extensions = ArazzoModelCopy.CopyExtensions(original.Extensions),
        };

        // Assert
        ArazzoJsonWriter.Write(copy).ShouldBe(ArazzoJsonWriter.Write(original));
        copy.Workflows[0].ShouldNotBeSameAs(workflow);
        copy.Workflows[0].Steps[0].Parameters![0].Value!.Value.Selector.ShouldNotBeSameAs(selector);
        copy.Workflows[0].Steps[0].RequestBody!.Payload!.Literal.ShouldNotBeSameAs(workflow.Steps[0].RequestBody!.Payload!.Literal);
        copy.Workflows[0].Steps[0].OnFailure![0].Value!.Criteria![0].ShouldNotBeSameAs(criterion);
        copy.Components!.Parameters!["tenant"].ShouldNotBeSameAs(parameter);
        copy.Extensions!["x-d"].ShouldNotBeSameAs(original.Extensions!["x-d"]);
    }

    [Fact]
    public void AddDocumentTransformer_RejectsNull()
    {
        var options = new ArazzoOptions();
        Should.Throw<ArgumentNullException>(() => options.AddDocumentTransformer((IArazzoDocumentTransformer)null!));
        Should.Throw<ArgumentNullException>(() => options.AddDocumentTransformer(
            (Func<ArazzoDocument, ArazzoDocumentTransformerContext, CancellationToken, Task>)null!));
    }

    private sealed class SummaryAppender(string suffix) : IArazzoDocumentTransformer
    {
        public Task TransformAsync(ArazzoDocument document, ArazzoDocumentTransformerContext context, CancellationToken cancellationToken)
        {
            document.Info.Summary += suffix;
            return Task.CompletedTask;
        }
    }

    public sealed class DisposalTracker
    {
        public int Disposed { get; set; }
    }

    private sealed class WorkflowAddingTransformer(DisposalTracker tracker) : IArazzoDocumentTransformer, IDisposable
    {
        public Task TransformAsync(ArazzoDocument document, ArazzoDocumentTransformerContext context, CancellationToken cancellationToken)
        {
            document.Workflows.Add(new ArazzoWorkflow
            {
                WorkflowId = "added",
                Steps = [new ArazzoStep { StepId = "s", OperationId = "getOrder" }],
            });
            return Task.CompletedTask;
        }

        public void Dispose() => tracker.Disposed++;
    }
}
