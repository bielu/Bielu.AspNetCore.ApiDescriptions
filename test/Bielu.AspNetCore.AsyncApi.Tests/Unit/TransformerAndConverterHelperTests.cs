using System.Text.Json;
using System.Text.Json.Nodes;
using Bielu.AspNetCore.AsyncApi.Schemas;
using Bielu.AspNetCore.AsyncApi.Services;
using Bielu.AspNetCore.AsyncApi.Transformers;
using ByteBard.AsyncAPI.Models;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Bielu.AspNetCore.AsyncApi.Tests.Unit;

public class TransformerAndConverterHelperTests
{
    private static readonly JsonSerializerOptions ConverterOptions = new() { Converters = { new AsyncApiAnyJsonConverter() } };

    [Fact]
    public void AsyncApiAnyJsonConverter_ReadObject_WrapsTheNode()
    {
        // Act
        var value = JsonSerializer.Deserialize<AsyncApiAny>("""{ "speed": 42 }""", ConverterOptions);

        // Assert
        value.ShouldNotBeNull().GetNode()!["speed"]!.GetValue<int>().ShouldBe(42);
    }

    [Fact]
    public void AsyncApiAnyJsonConverter_ReadNull_ReturnsNull()
    {
        JsonSerializer.Deserialize<AsyncApiAny>("null", ConverterOptions).ShouldBeNull();
    }

    [Fact]
    public void AsyncApiAnyJsonConverter_Write_WritesTheWrappedNode()
    {
        // Arrange
        var value = new AsyncApiAny(new JsonObject { ["unit"] = "lux" });

        // Act
        var json = JsonSerializer.Serialize(value, ConverterOptions);

        // Assert
        json.ShouldBe("""{"unit":"lux"}""");
    }

    [Fact]
    public void AsyncApiAnyJsonConverter_WriteNullNode_WritesJsonNull()
    {
        // Arrange
        var value = new AsyncApiAny((JsonNode?)null!);

        // Act
        var json = JsonSerializer.Serialize(value, ConverterOptions);

        // Assert
        json.ShouldBe("null");
    }

    [Fact]
    public void AsyncApiAnyJsonConverter_WriteNullArguments_Throws()
    {
        // Arrange
        var converter = new AsyncApiAnyJsonConverter();
        using var stream = new MemoryStream();
        using var writer = new Utf8JsonWriter(stream);

        // Act & Assert
        Should.Throw<ArgumentNullException>(() => converter.Write(null!, new AsyncApiAny(new JsonObject()), ConverterOptions));
        Should.Throw<ArgumentNullException>(() => converter.Write(writer, null!, ConverterOptions));
    }

    [Fact]
    public async Task DelegateDocumentTransformer_TransformAsync_CallsTheDelegate()
    {
        // Arrange
        var document = new AsyncApiDocument();
        AsyncApiDocument? seen = null;
        var transformer = new DelegateAsyncApiDocumentTransformer((d, _, _) =>
        {
            seen = d;
            return Task.CompletedTask;
        });

        // Act
        await transformer.TransformAsync(document, null!, CancellationToken.None);

        // Assert
        seen.ShouldBeSameAs(document);
    }

    [Fact]
    public async Task AddSerializedDocumentTransformer_Delegate_IsRegisteredAndRuns()
    {
        // Arrange
        var options = new AsyncApiOptions();
        var context = new AsyncApiSerializedDocumentContext
        {
            DocumentName = "v1",
            Format = AsyncApiDocumentFormat.Json,
            ApplicationServices = new ServiceCollection().BuildServiceProvider(),
        };

        // Act
        var returned = options.AddSerializedDocumentTransformer((document, ctx, _) =>
            ValueTask.FromResult($"{document}:{ctx.DocumentName}"));
        var result = await options.SerializedDocumentTransformers.ShouldHaveSingleItem()
            .TransformAsync("{}", context, CancellationToken.None);

        // Assert
        returned.ShouldBeSameAs(options);
        result.ShouldBe("{}:v1");
    }

    [Fact]
    public void AddSerializedDocumentTransformer_NullDelegate_Throws()
    {
        Should.Throw<ArgumentNullException>(() => new AsyncApiOptions().AddSerializedDocumentTransformer(
            (Func<string, AsyncApiSerializedDocumentContext, CancellationToken, ValueTask<string>>)null!));
    }

    [Fact]
    public async Task FinalizeTransformerAsync_AsyncDisposable_IsDisposedAsynchronously()
    {
        // Arrange
        var transformer = new BothDisposable();

        // Act
        await transformer.FinalizeTransformerAsync();

        // Assert
        transformer.DisposedAsync.ShouldBeTrue();
        transformer.Disposed.ShouldBeFalse();
    }

    [Fact]
    public async Task FinalizeTransformerAsync_Disposable_IsDisposed()
    {
        // Arrange
        var transformer = new OnlyDisposable();

        // Act
        await transformer.FinalizeTransformerAsync();

        // Assert
        transformer.Disposed.ShouldBeTrue();
    }

    [Fact]
    public async Task FinalizeTransformerAsync_NotDisposable_DoesNothing()
    {
        await Should.NotThrowAsync(() => new object().FinalizeTransformerAsync());
    }

    private sealed class OnlyDisposable : IDisposable
    {
        public bool Disposed { get; private set; }

        public void Dispose() => Disposed = true;
    }

    private sealed class BothDisposable : IDisposable, IAsyncDisposable
    {
        public bool Disposed { get; private set; }

        public bool DisposedAsync { get; private set; }

        public void Dispose() => Disposed = true;

        public ValueTask DisposeAsync()
        {
            DisposedAsync = true;
            return ValueTask.CompletedTask;
        }
    }
}
