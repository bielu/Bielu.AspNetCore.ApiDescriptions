using Bielu.Arazzo.Models;
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
