using System.Net;
using Google.Protobuf.Reflection;
using Microsoft.AspNetCore.Mvc.Testing;
using Shouldly;
using Xunit;

namespace Bielu.AspNetCore.AsyncApi.Scalar.Grpc.Tests;

/// <summary>
/// Boots the GrpcGreeter example application (which calls <c>MapScalarGrpcAssets()</c>) and
/// verifies the console bundle and the protobuf descriptor endpoint it serves.
/// </summary>
public class ScalarGrpcEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public ScalarGrpcEndpointTests(WebApplicationFactory<Program> factory) => _factory = factory;

    [Fact]
    public async Task PluginBundle_IsServed()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/bielu/scalar/grpc/plugin.js");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("text/javascript");
        var body = await response.Content.ReadAsStringAsync();
        body.ShouldNotBeNullOrWhiteSpace();
        // The bundle registers the console custom element; its tag is a stable marker.
        body.ShouldContain("bielu-grpc-console");
    }

    [Fact]
    public async Task Descriptors_RoundTripToFileDescriptorSet()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/bielu/scalar/grpc/descriptors");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/x-protobuf");

        var descriptorSet = FileDescriptorSet.Parser.ParseFrom(await response.Content.ReadAsByteArrayAsync());

        // The greeter service, its methods and its messages must all be present.
        var greetFile = descriptorSet.File.ShouldHaveSingleItem(); // greet.proto has no imports
        greetFile.Package.ShouldBe("greet");
        var service = greetFile.Service.ShouldHaveSingleItem();
        service.Name.ShouldBe("Greeter");
        service.Method.Select(method => method.Name).ShouldBe(["SayHello", "SayHellos"], ignoreOrder: true);
        greetFile.MessageType.Select(message => message.Name)
            .ShouldBe(["HelloRequest", "HelloReply"], ignoreOrder: true);

        // The wire contract of the console: field numbers survive the round trip.
        greetFile.MessageType.Single(message => message.Name == "HelloRequest")
            .Field.ShouldHaveSingleItem().Number.ShouldBe(1);
    }
}
