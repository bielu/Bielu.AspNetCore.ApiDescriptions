using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using Bielu.AspNetCore.Arazzo.Services;
using Shouldly;
using Xunit;

namespace Bielu.AspNetCore.Arazzo.Tests.Unit;

public class ArazzoWorkflowBuilderTests
{
    private sealed record OrderInputs(string Sku, int Quantity);

    private static JsonObject BuildInputs(Action<ArazzoWorkflowBuilder> configure)
    {
        var options = new ArazzoOptions();
        options.AddOpenApiSource("api", "v1");
        options.AddWorkflow("wf", wf =>
        {
            configure(wf);
            wf.Step("step", s => s.Operation("createOrder"));
        });
        return options.Workflows[0].Inputs.ShouldBeOfType<JsonObject>();
    }

    [Fact]
    public void WithInputs_WithoutOptions_UsesDefaultNaming()
    {
        // Arrange & Act
        var inputs = BuildInputs(wf => wf.WithInputs<OrderInputs>());

        // Assert
        var properties = inputs["properties"].ShouldBeOfType<JsonObject>();
        properties.ContainsKey("Sku").ShouldBeTrue();
        properties.ContainsKey("Quantity").ShouldBeTrue();
    }

    [Fact]
    public void WithInputs_OptionsWithoutTypeInfoResolver_UsesDefaultResolver()
    {
        // Arrange
        var serializerOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);

        // Act
        var inputs = BuildInputs(wf => wf.WithInputs<OrderInputs>(serializerOptions));

        // Assert
        var properties = inputs["properties"].ShouldBeOfType<JsonObject>();
        properties.ContainsKey("sku").ShouldBeTrue();
        properties.ContainsKey("quantity").ShouldBeTrue();
    }

    [Fact]
    public void WithInputs_OptionsWithoutTypeInfoResolver_LeavesCallerOptionsUnchanged()
    {
        // Arrange
        var serializerOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);

        // Act
        BuildInputs(wf => wf.WithInputs<OrderInputs>(serializerOptions));

        // Assert
        serializerOptions.TypeInfoResolver.ShouldBeNull();
        serializerOptions.IsReadOnly.ShouldBeFalse();
    }

    [Fact]
    public void WithInputs_OptionsWithTypeInfoResolver_UsesThatResolver()
    {
        // Arrange
        var resolver = new DefaultJsonTypeInfoResolver();
        resolver.Modifiers.Add(typeInfo =>
        {
            if (typeInfo.Type == typeof(OrderInputs))
            {
                typeInfo.Properties.Single(p => p.Name == "sku").Name = "stockKeepingUnit";
            }
        });
        var serializerOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web) { TypeInfoResolver = resolver };

        // Act
        var inputs = BuildInputs(wf => wf.WithInputs<OrderInputs>(serializerOptions));

        // Assert
        var properties = inputs["properties"].ShouldBeOfType<JsonObject>();
        properties.ContainsKey("stockKeepingUnit").ShouldBeTrue();
        properties.ContainsKey("sku").ShouldBeFalse();
    }

    [Fact]
    public void WithInputs_JsonTypeInfo_ThrowsForNull()
    {
        // Arrange
        var options = new ArazzoOptions();

        // Act & Assert
        Should.Throw<ArgumentNullException>(() => options.AddWorkflow("wf", wf => wf.WithInputs((JsonTypeInfo)null!)));
    }
}
