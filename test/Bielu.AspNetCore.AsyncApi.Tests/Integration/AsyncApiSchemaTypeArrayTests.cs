using Bielu.AspNetCore.AsyncApi.Extensions;
using Bielu.AspNetCore.AsyncApi.Services;
using ByteBard.AsyncAPI.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Bielu.AspNetCore.AsyncApi.Tests.Integration;

/// <summary>
/// System.Text.Json describes nullable values and, under ASP.NET Core's web defaults
/// (<c>JsonNumberHandling.AllowReadingFromString</c>), numbers with a type array. Generated schemas
/// must keep nullability and keep numbers numeric.
/// </summary>
public class AsyncApiSchemaTypeArrayTests
{
    public sealed record Shapes(int Count, int? MaybeCount, string? Note, string Name, double Ratio);

    [Fact]
    public async Task GetOrCreateSchemaAsync_NullableReference_KeepsNullInType()
    {
        // Act
        var schema = await GetSchemaAsync();

        // Assert
        schema.Properties["note"].ShouldBeOfType<AsyncApiJsonSchema>().Type.ShouldBe(SchemaType.String | SchemaType.Null);
        schema.Properties["name"].ShouldBeOfType<AsyncApiJsonSchema>().Type.ShouldBe(SchemaType.String);
    }

    [Fact]
    public async Task GetOrCreateSchemaAsync_NullableNumber_KeepsNullAndStaysNumeric()
    {
        // Act
        var schema = await GetSchemaAsync();

        // Assert
        var maybeCount = schema.Properties["maybeCount"].ShouldBeOfType<AsyncApiJsonSchema>();
        maybeCount.Type.ShouldBe(SchemaType.Integer | SchemaType.Null);
        maybeCount.Format.ShouldBe("int32");
    }

    [Fact]
    public async Task GetOrCreateSchemaAsync_NumberReadableFromString_IsDocumentedAsNumber()
    {
        // Act
        var schema = await GetSchemaAsync();

        // Assert
        var count = schema.Properties["count"].ShouldBeOfType<AsyncApiJsonSchema>();
        count.Type.ShouldBe(SchemaType.Integer);
        count.Pattern.ShouldBeNull();
        schema.Properties["ratio"].ShouldBeOfType<AsyncApiJsonSchema>().Type.ShouldBe(SchemaType.Number);
    }

    private static async Task<AsyncApiJsonSchema> GetSchemaAsync()
    {
        // Arrange
        AsyncApiJsonSchema? captured = null;
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddAsyncApi(options => options.AddDocumentTransformer(async (_, context, cancellationToken) =>
            captured = await context.GetOrCreateSchemaAsync(typeof(Shapes), cancellationToken: cancellationToken)));

        await using var app = builder.Build();
        await app.StartAsync();

        await app.Services.GetRequiredKeyedService<IAsyncApiDocumentProvider>(AsyncApiGeneratorConstants.DefaultDocumentName)
            .GetAsyncApiDocumentAsync();
        await app.StopAsync();

        return captured.ShouldNotBeNull();
    }
}
