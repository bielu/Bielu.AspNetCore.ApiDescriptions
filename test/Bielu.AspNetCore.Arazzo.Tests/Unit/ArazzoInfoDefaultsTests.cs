using Bielu.Arazzo.Models;
using Bielu.AspNetCore.Arazzo.Extensions;
using Bielu.AspNetCore.Arazzo.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;
using Xunit;

namespace Bielu.AspNetCore.Arazzo.Tests.Unit;

public class ArazzoInfoDefaultsTests
{
    private static Task<ArazzoDocument> BuildAsync(Action<ArazzoOptions> configure, string? applicationName = null)
    {
        var services = new ServiceCollection();
        if (applicationName is not null)
        {
            services.AddSingleton<IHostEnvironment>(new FakeHostEnvironment { ApplicationName = applicationName });
        }

        services.AddArazzo("orders", options =>
        {
            options.ValidateSourceReferencesOnStartup = false;
            options.AddWorkflow("wf", wf => wf.Step("step", s => s.Workflow("wf")));
            configure(options);
        });
        return services.BuildServiceProvider()
            .GetRequiredKeyedService<IArazzoDocumentProvider>("orders")
            .GetArazzoDocumentAsync();
    }

    [Fact]
    public async Task Document_WithoutWithInfo_DefaultsTheInfoLikeAsyncApi()
    {
        // Act
        var document = await BuildAsync(_ => { }, "Shop.Api");

        // Assert
        document.Info.Title.ShouldBe("Shop.Api | orders");
        document.Info.Version.ShouldBe(ArazzoDefaults.DefaultInfoVersion);
    }

    [Fact]
    public async Task Document_WithoutWithInfoOrHostEnvironment_UsesTheDocumentNameAsTitle()
    {
        // Act
        var document = await BuildAsync(_ => { });

        // Assert
        document.Info.Title.ShouldBe("orders");
    }

    [Fact]
    public async Task WithDescription_BeforeWithInfo_KeepsTheDescription()
    {
        // Act
        var document = await BuildAsync(options => options
            .WithDescription("Order workflows.")
            .WithInfo(info => info.Version = "2.0.0"));

        // Assert
        document.Info.Description.ShouldBe("Order workflows.");
        document.Info.Version.ShouldBe("2.0.0");
        document.Info.Title.ShouldBe("orders");
    }

    [Fact]
    public void WithDescription_RejectsNull()
    {
        Should.Throw<ArgumentNullException>(() => new ArazzoOptions().WithDescription(null!));
    }

    private sealed class FakeHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";

        public string ApplicationName { get; set; } = string.Empty;

        public string ContentRootPath { get; set; } = string.Empty;

        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
    }
}
