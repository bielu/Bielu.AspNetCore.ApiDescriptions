using Bielu.AspNetCore.AsyncApi.Extensions.Protocols.SignalR;
using Bielu.AspNetCore.AsyncApi.Services;
using Shouldly;
using Xunit;

namespace Bielu.AspNetCore.AsyncApi.Extensions.Protocols.SignalR.Tests;

public class SignalROptionsExtensionsTests
{
    [Fact]
    public void AddSignalRChannelBinding_RegistersConfiguredBinding()
    {
        var options = new AsyncApiOptions();

        options.AddSignalRChannelBinding("chatHub", b =>
        {
            b.Hub = "/chatHub";
            b.Transports.Add(SignalRProtocol.Transports.WebSockets);
        });

        var binding = options.ChannelBindings["chatHub"].ShouldHaveSingleItem()
            .ShouldBeOfType<SignalRChannelBinding>();
        binding.Hub.ShouldBe("/chatHub");
        binding.Transports.ShouldContain(SignalRProtocol.Transports.WebSockets);
    }

    [Fact]
    public void AddSignalROperationBinding_RegistersConfiguredBinding()
    {
        var options = new AsyncApiOptions();

        options.AddSignalROperationBinding("SendMessage", b =>
        {
            b.Target = "SendMessage";
            b.Direction = SignalRProtocol.Directions.ClientToServer;
            b.CallType = SignalRProtocol.CallTypes.Invocation;
        });

        var binding = options.OperationBindings["SendMessage"].ShouldHaveSingleItem()
            .ShouldBeOfType<SignalROperationBinding>();
        binding.Target.ShouldBe("SendMessage");
        binding.Direction.ShouldBe("clientToServer");
        binding.CallType.ShouldBe("invocation");
    }

    [Fact]
    public void AddSignalRChannelBinding_ReturnsOptionsForChaining()
    {
        var options = new AsyncApiOptions();

        options.AddSignalRChannelBinding("hub").ShouldBeSameAs(options);
    }
}
