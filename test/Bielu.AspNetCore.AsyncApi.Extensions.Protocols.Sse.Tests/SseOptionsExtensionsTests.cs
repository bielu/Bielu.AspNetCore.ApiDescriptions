using Bielu.AspNetCore.AsyncApi.Extensions.Protocols.Sse;
using Bielu.AspNetCore.AsyncApi.Services;
using Shouldly;
using Xunit;

namespace Bielu.AspNetCore.AsyncApi.Extensions.Protocols.Sse.Tests;

public class SseOptionsExtensionsTests
{
    [Fact]
    public void AddSseChannelBinding_RegistersConfiguredBinding()
    {
        var options = new AsyncApiOptions();

        options.AddSseChannelBinding("events", b =>
        {
            b.Path = "/events";
            b.Method = SseProtocol.Methods.Get;
        });

        var binding = options.ChannelBindings["events"].ShouldHaveSingleItem()
            .ShouldBeOfType<SseChannelBinding>();
        binding.Path.ShouldBe("/events");
        binding.Method.ShouldBe("GET");
    }

    [Fact]
    public void AddSseOperationBinding_RegistersConfiguredBinding()
    {
        var options = new AsyncApiOptions();

        options.AddSseOperationBinding("onPriceUpdate", b =>
        {
            b.Direction = SseProtocol.Directions.ServerToClient;
        });

        var binding = options.OperationBindings["onPriceUpdate"].ShouldHaveSingleItem()
            .ShouldBeOfType<SseOperationBinding>();
        binding.Direction.ShouldBe("serverToClient");
    }

    [Fact]
    public void AddSseChannelBinding_ReturnsOptionsForChaining()
    {
        var options = new AsyncApiOptions();

        options.AddSseChannelBinding("events").ShouldBeSameAs(options);
    }
}
