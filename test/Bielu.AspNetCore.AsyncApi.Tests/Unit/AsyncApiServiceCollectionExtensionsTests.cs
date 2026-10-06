using Bielu.AspNetCore.AsyncApi.Extensions;
using Bielu.AspNetCore.AsyncApi.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Bielu.AspNetCore.AsyncApi.Tests.Unit;

/// <summary>
/// Unit tests for AsyncApiServiceCollectionExtensions.
/// </summary>
public class AsyncApiServiceCollectionExtensionsTests
{
    [Fact]
    public void AddAsyncApi_RegistersRequiredServices()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddAsyncApi();
        var provider = services.BuildServiceProvider();

        // Assert - Check that options are registered
        var optionsMonitor = provider.GetService<IOptionsMonitor<AsyncApiOptions>>();
        optionsMonitor.ShouldNotBeNull();
    }

    [Fact]
    public void AddAsyncApi_WithDocumentName_RegistersNamedServices()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddAsyncApi("custom-doc");
        var provider = services.BuildServiceProvider();

        // Assert
        var optionsMonitor = provider.GetRequiredService<IOptionsMonitor<AsyncApiOptions>>();
        var options = optionsMonitor.Get("custom-doc");
        options.ShouldNotBeNull();
    }

    [Fact]
    public void AddAsyncApi_WithOptions_ConfiguresOptions()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddAsyncApi(options =>
        {
            options.AddServer("test-server", "localhost", "http");
            options.WithDefaultContentType("application/json");
        });
        var provider = services.BuildServiceProvider();

        // Assert
        var optionsMonitor = provider.GetRequiredService<IOptionsMonitor<AsyncApiOptions>>();
        var asyncApiOptions = optionsMonitor.Get("v1"); // Default document name is "v1"

        asyncApiOptions.DefaultContentType.ShouldBe("application/json");
    }

    [Fact]
    public void AddAsyncApi_WithNamedOptions_ConfiguresCorrectDocument()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddAsyncApi("api-v1", options =>
        {
            options.WithInfo("API V1", "1.0.0");
        });
        services.AddAsyncApi("api-v2", options =>
        {
            options.WithInfo("API V2", "2.0.0");
        });
        var provider = services.BuildServiceProvider();

        // Assert
        var optionsMonitor = provider.GetRequiredService<IOptionsMonitor<AsyncApiOptions>>();

        var v1Options = optionsMonitor.Get("api-v1");
        v1Options.Info!.Title.ShouldBe("API V1");
        v1Options.Info.Version.ShouldBe("1.0.0");

        var v2Options = optionsMonitor.Get("api-v2");
        v2Options.Info!.Title.ShouldBe("API V2");
        v2Options.Info.Version.ShouldBe("2.0.0");
    }

    [Fact]
    public void AddAsyncApi_DocumentNameIsCaseInsensitive()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddAsyncApi("MyDocument", options =>
        {
            options.WithInfo("My Document", "1.0.0");
        });
        var provider = services.BuildServiceProvider();

        // Assert - Document name should be lowercased
        var optionsMonitor = provider.GetRequiredService<IOptionsMonitor<AsyncApiOptions>>();
        var options = optionsMonitor.Get("mydocument");
        options.Info!.Title.ShouldBe("My Document");
    }

    [Fact]
    public void AddAsyncApi_ThrowsForNullServices()
    {
        // Arrange
        IServiceCollection? services = null;

        // Act & Assert
        Should.Throw<ArgumentNullException>(() => services!.AddAsyncApi());
    }

    [Fact]
    public void AddAsyncApi_ThrowsForNullConfigureOptions()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act & Assert
        Should.Throw<ArgumentNullException>(() => services.AddAsyncApi("test", null!));
    }

    [Fact]
    public void AddAsyncApi_MultipleCalls_RegistersMultipleDocuments()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddAsyncApi("doc1", options => options.WithInfo("Doc 1", "1.0.0"));
        services.AddAsyncApi("doc2", options => options.WithInfo("Doc 2", "1.0.0"));
        services.AddAsyncApi("doc3", options => options.WithInfo("Doc 3", "1.0.0"));
        var provider = services.BuildServiceProvider();

        // Assert
        var optionsMonitor = provider.GetRequiredService<IOptionsMonitor<AsyncApiOptions>>();

        optionsMonitor.Get("doc1").Info!.Title.ShouldBe("Doc 1");
        optionsMonitor.Get("doc2").Info!.Title.ShouldBe("Doc 2");
        optionsMonitor.Get("doc3").Info!.Title.ShouldBe("Doc 3");
    }

    [Fact]
    public void AddAsyncApi_DefaultDocumentName_UsesV1()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddAsyncApi(options =>
        {
            options.WithInfo("Default Document", "1.0.0");
        });
        var provider = services.BuildServiceProvider();

        // Assert - Default document name is "v1"
        var optionsMonitor = provider.GetRequiredService<IOptionsMonitor<AsyncApiOptions>>();
        var options = optionsMonitor.Get("v1");
        options.Info!.Title.ShouldBe("Default Document");
    }
}
