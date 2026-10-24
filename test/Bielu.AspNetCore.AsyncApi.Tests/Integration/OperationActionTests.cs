using Bielu.AspNetCore.AsyncApi.Attributes;
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
/// <see cref="SendOperationAttribute"/> and <see cref="ReceiveOperationAttribute"/> must produce the matching
/// AsyncAPI 3 action, and the obsolete 2.x-named attributes must keep producing the action they always did.
/// </summary>
public class OperationActionTests
{
    private const string DocumentName = "operation-action";

    public sealed record LightMeasured(int Lumens);

    [AsyncApi(DocumentName)]
    public sealed class Channels
    {
        [Channel("light/sent")]
        [SendOperation(typeof(LightMeasured), OperationId = "sendOperation")]
        public void Send()
        {
        }

        [Channel("light/received")]
        [ReceiveOperation(typeof(LightMeasured), OperationId = "receiveOperation")]
        public void Receive()
        {
        }

#pragma warning disable CS0618 // The obsolete attributes are what is under test.
        [Channel("light/subscribed")]
        [SubscribeOperation(typeof(LightMeasured), OperationId = "subscribeOperation")]
        public void Subscribe()
        {
        }

        [Channel("light/published")]
        [PublishOperation(typeof(LightMeasured), OperationId = "publishOperation")]
        public void Publish()
        {
        }
#pragma warning restore CS0618
    }

    [Theory]
    [InlineData("sendOperation", AsyncApiAction.Send)]
    [InlineData("receiveOperation", AsyncApiAction.Receive)]
    [InlineData("subscribeOperation", AsyncApiAction.Send)]
    [InlineData("publishOperation", AsyncApiAction.Receive)]
    public async Task GetAsyncApiDocument_OperationAttribute_SetsMatchingAction(string operationId, AsyncApiAction expected)
    {
        // Act
        var document = await GetDocumentAsync();

        // Assert
        document.Operations[operationId].Action.ShouldBe(expected);
    }

    [Fact]
    public void OperationAttributes_SetOperationTypeFromTheApplicationsPointOfView()
    {
        // Act & Assert
        new SendOperationAttribute().OperationType.ShouldBe(OperationType.Send);
        new SendOperationAttribute(typeof(LightMeasured)).OperationType.ShouldBe(OperationType.Send);
        new ReceiveOperationAttribute().OperationType.ShouldBe(OperationType.Receive);
        new ReceiveOperationAttribute(typeof(LightMeasured), "tag").OperationType.ShouldBe(OperationType.Receive);
#pragma warning disable CS0618 // The obsolete attributes are what is under test.
        new SubscribeOperationAttribute(typeof(LightMeasured)).OperationType.ShouldBe(OperationType.Send);
        new PublishOperationAttribute(typeof(LightMeasured), "tag").OperationType.ShouldBe(OperationType.Receive);
#pragma warning restore CS0618
    }

    private static async Task<AsyncApiDocument> GetDocumentAsync()
    {
        // Arrange
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddAsyncApi(DocumentName);

        await using var app = builder.Build();
        await app.StartAsync();

        var document = await app.Services.GetRequiredKeyedService<IAsyncApiDocumentProvider>(DocumentName)
            .GetAsyncApiDocumentAsync();
        await app.StopAsync();

        return document;
    }
}
