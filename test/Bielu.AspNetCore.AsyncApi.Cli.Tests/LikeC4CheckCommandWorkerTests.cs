using Bielu.AspNetCore.AsyncApi.Cli.Commands;
using Shouldly;
using Xunit;

namespace Bielu.AspNetCore.AsyncApi.Cli.Tests;

public sealed class LikeC4CheckCommandWorkerTests : IDisposable
{
    private const string Orders = """
        {
          "asyncapi": "3.0.0",
          "info": { "title": "Orders", "version": "1" },
          "channels": { "c": { "address": "orders.created", "messages": { "m": { "name": "OrderCreated" } } } },
          "operations": { "s": { "action": "send", "channel": { "$ref": "#/channels/c" } } }
        }
        """;

    private const string Inventory = """
        {
          "asyncapi": "3.0.0",
          "info": { "title": "Inventory", "version": "1" },
          "channels": { "c": { "address": "orders.created", "messages": { "m": { "name": "OrderCreated" } } } },
          "operations": { "r": { "action": "receive", "channel": { "$ref": "#/channels/c" } } }
        }
        """;

    private const string Elements = """
        "elements": {
          "orders": { "title": "Orders", "metadata": { "asyncapi": "orders" } },
          "inventory": { "title": "Inventory", "metadata": { "asyncapi": "inventory" } }
        }
        """;

    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"asyncapi_cli_likec4_check_{Guid.NewGuid():N}");
    private readonly List<string> _info = [];
    private readonly List<string> _warnings = [];
    private readonly List<string> _errors = [];

    public LikeC4CheckCommandWorkerTests()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(Path.Combine(_directory, "orders.asyncapi.json"), Orders);
        File.WriteAllText(Path.Combine(_directory, "inventory.asyncapi.json"), Inventory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private int Run(string model, Action<LikeC4CheckCommandContext>? configure = null)
    {
        var modelPath = Path.Combine(_directory, "likec4.json");
        File.WriteAllText(modelPath, model);
        var context = new LikeC4CheckCommandContext { ModelPath = modelPath };
        context.Sources.Add(Path.Combine(_directory, "*.asyncapi.json"));
        configure?.Invoke(context);
        return new LikeC4CheckCommandWorker(context, _info.Add, _warnings.Add, _errors.Add).Process();
    }

    [Fact]
    public void Process_ModelThatAgreesWithTheDocuments_Succeeds()
    {
        // Arrange
        var model = $$"""{ {{Elements}}, "relations": { "r": { "source": { "model": "orders" }, "target": { "model": "inventory" } } } }""";

        // Act
        var exitCode = Run(model);

        // Assert
        exitCode.ShouldBe(0, string.Join('\n', _errors));
        _info.ShouldContain(line => line.StartsWith("Checking ", StringComparison.Ordinal));
        _info.ShouldContain("  OK");
    }

    [Fact]
    public void Process_ModelMissingAFlow_FailsWithTheFinding()
    {
        // Arrange
        var model = $$"""{ {{Elements}}, "relations": {} }""";

        // Act
        var exitCode = Run(model);

        // Assert
        exitCode.ShouldBe(1);
        _errors.ShouldHaveSingleItem().ShouldBe(
            "  Error: Service 'orders' sends OrderCreated to service 'inventory' through channel 'orders.created', but the model has no relationship between them. (at model)");
    }

    [Fact]
    public void Process_UnmodelledDocument_WarnsAndFailsOnlyWhenStrict()
    {
        // Arrange
        var model = """{ "elements": { "orders": { "metadata": { "asyncapi": "orders" } } }, "relations": {} }""";

        // Act
        var lenient = Run(model);
        var strict = Run(model, context => context.Strict = true);

        // Assert
        lenient.ShouldBe(0);
        strict.ShouldBe(1);
        _warnings.ShouldHaveSingleItem().ShouldContain("No element stands for AsyncAPI document 'inventory'");
        _errors.ShouldHaveSingleItem().ShouldStartWith("  Warning (Strict): No element stands for AsyncAPI document 'inventory'");
    }

    [Fact]
    public void Process_ModelThatIsNotAnExport_ReportsIt()
    {
        // Act
        var exitCode = Run("[]");

        // Assert
        exitCode.ShouldBe(1);
        _errors.ShouldHaveSingleItem().ShouldContain("likec4 export json");
    }

    [Fact]
    public void Process_MissingModel_ReportsIt()
    {
        // Arrange
        var context = new LikeC4CheckCommandContext { ModelPath = Path.Combine(_directory, "missing.json") };
        context.Sources.Add(Path.Combine(_directory, "*.asyncapi.json"));

        // Act
        var exitCode = new LikeC4CheckCommandWorker(context, _info.Add, _warnings.Add, _errors.Add).Process();

        // Assert
        exitCode.ShouldBe(1);
        _errors.ShouldHaveSingleItem().ShouldStartWith("Could not read '");
    }

    [Fact]
    public void Process_GlobMatchingNoDocuments_ReportsIt()
    {
        // Act
        var exitCode = Run("{}", context =>
        {
            context.Sources.Clear();
            context.Sources.Add(Path.Combine(_directory, "*.nothing"));
        });

        // Assert
        exitCode.ShouldBe(1);
        _errors.ShouldBe(["No AsyncAPI documents found."]);
    }

    [Fact]
    public void Constructor_ThrowsForNullArguments()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() => new LikeC4CheckCommandWorker(null!, _ => { }, _ => { }, _ => { }));
        Should.Throw<ArgumentNullException>(() => new LikeC4CheckCommandWorker(new LikeC4CheckCommandContext(), null!, _ => { }, _ => { }));
        Should.Throw<ArgumentNullException>(() => new LikeC4CheckCommandWorker(new LikeC4CheckCommandContext(), _ => { }, null!, _ => { }));
        Should.Throw<ArgumentNullException>(() => new LikeC4CheckCommandWorker(new LikeC4CheckCommandContext(), _ => { }, _ => { }, null!));
    }
}
