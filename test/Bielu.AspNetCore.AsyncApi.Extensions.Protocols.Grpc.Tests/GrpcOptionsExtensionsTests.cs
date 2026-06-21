using Bielu.AspNetCore.AsyncApi.Extensions.Protocols.Grpc;
using Bielu.AspNetCore.AsyncApi.Services;
using Shouldly;
using Xunit;

namespace Bielu.AspNetCore.AsyncApi.Extensions.Protocols.Grpc.Tests;

public class GrpcOptionsExtensionsTests
{
    [Fact]
    public void AddGrpcChannelBinding_RegistersConfiguredBinding()
    {
        var options = new AsyncApiOptions();

        options.AddGrpcChannelBinding("greeter", b =>
        {
            b.Service = "greet.Greeter";
            b.Package = "greet";
        });

        var binding = options.ChannelBindings["greeter"].ShouldHaveSingleItem()
            .ShouldBeOfType<GrpcChannelBinding>();
        binding.Service.ShouldBe("greet.Greeter");
        binding.Package.ShouldBe("greet");
    }

    [Fact]
    public void AddGrpcOperationBinding_RegistersConfiguredBinding()
    {
        var options = new AsyncApiOptions();

        options.AddGrpcOperationBinding("sayHello", b =>
        {
            b.Method = "SayHello";
            b.MethodType = GrpcMethodType.Unary;
            b.IdempotencyLevel = GrpcProtocol.IdempotencyLevels.NoSideEffects;
        });

        var binding = options.OperationBindings["sayHello"].ShouldHaveSingleItem()
            .ShouldBeOfType<GrpcOperationBinding>();
        binding.Method.ShouldBe("SayHello");
        binding.MethodType.ShouldBe(GrpcMethodType.Unary);
        binding.IdempotencyLevel.ShouldBe("noSideEffects");
    }

    [Fact]
    public void AddGrpcChannelBinding_ReturnsOptionsForChaining()
    {
        var options = new AsyncApiOptions();

        options.AddGrpcChannelBinding("greeter").ShouldBeSameAs(options);
    }
}
