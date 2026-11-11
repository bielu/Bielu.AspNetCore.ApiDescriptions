using Bielu.AspNetCore.Arazzo.Validation;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Bielu.AspNetCore.Arazzo.Tests.Unit;

public class OpenApiDocumentRegistrationsTests
{
    private static ServiceProvider BuildProvider(params string[] documentNames)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        foreach (var documentName in documentNames)
        {
            services.AddOpenApi(documentName);
        }

        return services.BuildServiceProvider();
    }

    [Fact]
    public void TryGetRegisteredNames_ReadsEveryAddOpenApiName()
    {
        // Arrange
        using var provider = BuildProvider("v1", "internal");

        // Act
        var names = OpenApiDocumentRegistrations.TryGetRegisteredNames(provider);

        // Assert: null would mean the reflection no longer matches Microsoft.AspNetCore.OpenApi and the
        // keyed-lookup fallback is in use, which on ASP.NET Core 11 accepts any name.
        names.ShouldNotBeNull();
        names.ShouldBe(["v1", "internal"], ignoreOrder: true);
    }

    [Fact]
    public void IsRegistered_RegisteredName_ReturnsTrue()
    {
        // Arrange
        using var provider = BuildProvider("v1");

        // Act
        var registered = OpenApiDocumentRegistrations.IsRegistered(provider, "v1");

        // Assert
        registered.ShouldBeTrue();
    }

    [Fact]
    public void IsRegistered_UnregisteredName_ReturnsFalseEvenThoughKeyedLookupMaySucceed()
    {
        // Arrange
        using var provider = BuildProvider("v1");

        // Act
        var registered = OpenApiDocumentRegistrations.IsRegistered(provider, "v2");

        // Assert
        registered.ShouldBeFalse();
    }

    [Fact]
    public void IsRegistered_NoOpenApiAtAll_ReturnsFalse()
    {
        // Arrange
        using var provider = new ServiceCollection().BuildServiceProvider();

        // Act
        var registered = OpenApiDocumentRegistrations.IsRegistered(provider, "v1");

        // Assert
        registered.ShouldBeFalse();
    }

    [Fact]
    public void IsRegistered_ThrowsForNullServiceProvider()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() => OpenApiDocumentRegistrations.IsRegistered(null!, "v1"));
    }

    [Fact]
    public void IsRegistered_ThrowsForNullDocumentName()
    {
        // Arrange
        using var provider = BuildProvider("v1");

        // Act & Assert
        Should.Throw<ArgumentNullException>(() => OpenApiDocumentRegistrations.IsRegistered(provider, null!));
    }
}
