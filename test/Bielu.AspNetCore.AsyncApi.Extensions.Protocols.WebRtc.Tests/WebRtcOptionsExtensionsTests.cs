using Bielu.AspNetCore.AsyncApi.Extensions.Protocols.WebRtc;
using Bielu.AspNetCore.AsyncApi.Services;
using Shouldly;
using Xunit;

namespace Bielu.AspNetCore.AsyncApi.Extensions.Protocols.WebRtc.Tests;

public class WebRtcOptionsExtensionsTests
{
    [Fact]
    public void AddWebRtcChannelBinding_RegistersConfiguredBinding()
    {
        var options = new AsyncApiOptions();

        options.AddWebRtcChannelBinding("chat", b =>
        {
            b.ChannelType = WebRtcProtocol.ChannelTypes.DataChannel;
            b.Label = "chat";
            b.Ordered = true;
        });

        var binding = options.ChannelBindings["chat"].ShouldHaveSingleItem()
            .ShouldBeOfType<WebRtcChannelBinding>();
        binding.Label.ShouldBe("chat");
        binding.ChannelType.ShouldBe("dataChannel");
        binding.Ordered.ShouldBe(true);
    }

    [Fact]
    public void AddWebRtcOperationBinding_RegistersConfiguredBinding()
    {
        var options = new AsyncApiOptions();

        options.AddWebRtcOperationBinding("sendOffer", b =>
        {
            b.SignalingType = WebRtcSignalingType.Offer;
            b.Direction = WebRtcProtocol.Directions.ClientToServer;
        });

        var binding = options.OperationBindings["sendOffer"].ShouldHaveSingleItem()
            .ShouldBeOfType<WebRtcOperationBinding>();
        binding.SignalingType.ShouldBe(WebRtcSignalingType.Offer);
        binding.Direction.ShouldBe("clientToServer");
    }

    [Fact]
    public void AddWebRtcChannelBinding_ReturnsOptionsForChaining()
    {
        var options = new AsyncApiOptions();

        options.AddWebRtcChannelBinding("chat").ShouldBeSameAs(options);
    }
}
