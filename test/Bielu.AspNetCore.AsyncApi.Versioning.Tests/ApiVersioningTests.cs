namespace Bielu.AspNetCore.AsyncApi.Versioning.Tests;

using System.Net;
using Asp.Versioning;
using Bielu.AspNetCore.AsyncApi.Extensions;
using Bielu.AspNetCore.AsyncApi.Attributes.Attributes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;
using Xunit;

public class ApiVersioningTests
{
    [Fact]
    public async Task CanGetDocumentPerVersion()
    {
        // Arrange
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddControllers().AddApplicationPart(typeof(ApiVersioningTests).Assembly);
        builder.Services.AddApiVersioning(options =>
        {
            options.DefaultApiVersion = new ApiVersion(1, 0);
            options.AssumeDefaultVersionWhenUnspecified = true;
            options.ReportApiVersions = true;
        })
        .AddApiExplorer(options =>
        {
            options.GroupNameFormat = "'v'V";
            options.SubstituteApiVersionInUrl = true;
        });

        builder.Services.AddAsyncApiForApiVersions();
        
        var app = builder.Build();
        app.MapControllers();
        app.MapAsyncApi();
        
        await app.StartAsync();
        var client = app.GetTestServer().CreateClient();

        // Act
        var v1Response = await client.GetAsync("/asyncapi/v1.json");
        var v2Response = await client.GetAsync("/asyncapi/v2.json");

        // Assert
        v1Response.StatusCode.ShouldBe(HttpStatusCode.OK);
        v2Response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var v1Content = await v1Response.Content.ReadAsStringAsync();
        var v2Content = await v2Response.Content.ReadAsStringAsync();

        v1Content.ShouldContain("\"version\": \"1.0\"");
        v2Content.ShouldContain("\"version\": \"2.0\"");
        
        // V1 should contain TestV1 (as it matches all documents if not specified)
        v1Content.ShouldContain("api/v1/test");
        // V2 should contain TestV2
        v2Content.ShouldContain("api/v2/test");
        
        await app.StopAsync();
    }

    [Fact]
    public void GetDocumentNames_ReturnsAllVersions()
    {
        // Arrange
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddControllers().AddApplicationPart(typeof(ApiVersioningTests).Assembly);
        builder.Services.AddApiVersioning(options =>
        {
            options.DefaultApiVersion = new ApiVersion(1, 0);
        })
        .AddApiExplorer(options =>
        {
            options.GroupNameFormat = "'v'V";
        });

        builder.Services.AddAsyncApiForApiVersions();
        
        var app = builder.Build();
        var documentProvider = app.Services.GetRequiredService<Microsoft.Extensions.ApiDescriptions.IDocumentProvider>();

        // Act
        var names = documentProvider.GetDocumentNames();

        // Assert
        names.ShouldContain("v1");
        names.ShouldContain("v2");
    }
}

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/test")]
[AsyncApi]
[Channel("api/v1/test")]
public class TestV1Controller : ControllerBase
{
    [HttpGet]
    [Message(typeof(string))]
    public IActionResult Get() => Ok("v1");
}

[ApiController]
[ApiVersion("2.0")]
[Route("api/v{version:apiVersion}/test")]
[AsyncApi]
[Channel("api/v2/test")]
public class TestV2Controller : ControllerBase
{
    [HttpGet]
    [Message(typeof(string))]
    public IActionResult Get() => Ok("v2");
}
