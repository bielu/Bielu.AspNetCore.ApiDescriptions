using System.Net;
using System.Text.Json;
using Bielu.AspNetCore.AsyncApi.Attributes.Attributes;
using Bielu.AspNetCore.AsyncApi.Extensions;
using Bielu.AspNetCore.AsyncApi.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;
using Xunit;

namespace Bielu.AspNetCore.AsyncApi.Tests.Integration;

public class AsyncApiXmlDocumentationTests
{
    private const string TestDocumentName = "v1";

    [Fact]
    public async Task GetAsyncApiDocument_PopulatesDescriptionsFromXmlComments()
    {
        // Arrange
        var xmlPath = Path.Combine(AppContext.BaseDirectory, "Bielu.AspNetCore.AsyncApi.Tests.xml");
        
        // Ensure the XML file exists for the test (it might not be generated in all test environments)
        if (!File.Exists(xmlPath))
        {
            var xml = @"
<doc>
    <members>
        <member name=""T:Bielu.AspNetCore.AsyncApi.Tests.Integration.XmlDocTestBus"">
            <summary>Bus summary</summary>
        </member>
        <member name=""M:Bielu.AspNetCore.AsyncApi.Tests.Integration.XmlDocTestBus.ProcessMessage(Bielu.AspNetCore.AsyncApi.Tests.Integration.XmlDocTestMessage)"">
            <summary>Operation summary</summary>
            <remarks>Operation remarks</remarks>
            <param name=""message"">Message parameter</param>
        </member>
        <member name=""T:Bielu.AspNetCore.AsyncApi.Tests.Integration.XmlDocTestMessage"">
            <summary>Message summary</summary>
            <remarks>Message remarks</remarks>
        </member>
        <member name=""P:Bielu.AspNetCore.AsyncApi.Tests.Integration.XmlDocTestMessage.Text"">
            <summary>Text property description</summary>
        </member>
    </members>
</doc>";
            File.WriteAllText(xmlPath, xml);
        }

        using var host = await Host.CreateDefaultBuilder()
            .ConfigureWebHostDefaults(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.ConfigureServices(services =>
                {
                    services.AddControllers();
                    services.AddRouting();
                    services.AddAsyncApi(TestDocumentName, options =>
                    {
                        options.IncludeXmlComments(xmlPath);
                    });
                });
                webBuilder.Configure(app =>
                {
                    app.UseRouting();
                    app.UseEndpoints(endpoints =>
                    {
                        endpoints.MapAsyncApi();
                    });
                });
            })
            .StartAsync();

        var client = host.GetTestServer().CreateClient();

        // Act
        var response = await client.GetAsync($"/asyncapi/{TestDocumentName}.json");
        var content = await response.Content.ReadAsStringAsync();
        
        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var json = JsonDocument.Parse(content);
        var root = json.RootElement;

        // Check Channel Description (from type summary)
        var channels = root.GetProperty("channels");
        channels.TryGetProperty("xmlDocTestBus", out var channel).ShouldBeTrue();
        channel.GetProperty("description").GetString().ShouldBe("Bus summary");

        // Check Operation Summary/Description
        var operations = root.GetProperty("operations");
        operations.TryGetProperty("XmlDocTestBus_ProcessMessage_Publish", out var operation).ShouldBeTrue();
        operation.GetProperty("summary").GetString().ShouldBe("Operation summary");
        operation.GetProperty("description").GetString().ShouldBe("Operation remarks");

        // Check Message Summary/Description
        var messages = root.GetProperty("components").GetProperty("messages");
        messages.TryGetProperty("xmlDocTestMessage", out var message).ShouldBeTrue();
        message.GetProperty("summary").GetString().ShouldBe("Message summary");
        message.GetProperty("description").GetString().ShouldBe("Message remarks");

        // Check Schema Property Description
        var schemas = root.GetProperty("components").GetProperty("schemas");
        schemas.TryGetProperty("xmlDocTestMessage", out var schema).ShouldBeTrue();
        var properties = schema.GetProperty("properties");
        properties.GetProperty("text").GetProperty("description").GetString().ShouldBe("Text property description");
    }
}

[AsyncApi]
[Channel("xmlDocTestBus")]
public class XmlDocTestBus
{
    [PublishOperation(typeof(XmlDocTestMessage))]
    public void ProcessMessage(XmlDocTestMessage message) { }
}

public class XmlDocTestMessage
{
    public string Text { get; set; } = string.Empty;
}
