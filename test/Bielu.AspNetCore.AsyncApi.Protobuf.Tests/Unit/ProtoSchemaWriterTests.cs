using Bielu.AspNetCore.AsyncApi.Protobuf.Tests.Legacy;
using Bielu.AspNetCore.AsyncApi.Protobuf.Tests.Orders;
using Shouldly;
using Xunit;

namespace Bielu.AspNetCore.AsyncApi.Protobuf.Tests.Unit;

public class ProtoSchemaWriterTests
{
    [Fact]
    public void Write_Proto3Message_RendersTheWireContract()
    {
        // Act
        var source = ProtoSchemaWriter.Write(OrderPlaced.Descriptor);

        // Assert
        source.ShouldBe("""
            syntax = "proto3";

            package test.orders;

            import "google/protobuf/timestamp.proto";

            // @RootNode
            message OrderPlaced {
              message Meta {
                string trace = 1;
              }
              string id = 1;
              .google.protobuf.Timestamp placed_at = 2;
              repeated OrderLine lines = 3;
              map<string, string> attributes = 4;
              Status status = 5;
              optional string note = 6;
              oneof payment {
                string card = 7;
                string voucher = 8;
              }
              Address address = 9;
              OrderPlaced.Meta meta = 10;
              sint64 balance = 11;
              bytes signature = 12;
              reserved 13;
              reserved 15 to 20;
              reserved "legacy";
            }

            message OrderLine {
              string sku = 1;
              int32 quantity = 2;
            }

            enum Status {
              STATUS_UNSPECIFIED = 0;
              STATUS_PLACED = 1;
            }

            message Address {
              string city = 1;
            }

            """.ReplaceLineEndings("\n"));
    }

    [Fact]
    public void Write_LeavesOutTypesTheMessageDoesNotUse()
    {
        // Act & Assert
        ProtoSchemaWriter.Write(OrderPlaced.Descriptor).ShouldNotContain("Unrelated");
    }

    [Fact]
    public void Write_Proto2Message_KeepsItsLabels()
    {
        // Act
        var source = ProtoSchemaWriter.Write(LegacyEvent.Descriptor);

        // Assert
        source.ShouldStartWith("syntax = \"proto2\";");
        source.ShouldContain("  required string id = 1;\n");
        source.ShouldContain("  optional int32 count = 2;\n");
        source.ShouldContain("  repeated string tags = 3;\n");
    }

    [Fact]
    public void Write_NestedMessage_MarksItAndDeclaresItsParent()
    {
        // Act
        var source = ProtoSchemaWriter.Write(OrderPlaced.Types.Meta.Descriptor);

        // Assert
        source.ShouldContain("message OrderPlaced {");
        source.ShouldContain("  // @RootNode\n  message Meta {");
    }

    [Theory]
    [InlineData(typeof(OrderPlaced), "3")]
    [InlineData(typeof(LegacyEvent), "2")]
    public void SyntaxVersion_IsTheVersionOfTheFile(Type messageType, string expected)
    {
        // Arrange
        var descriptor = messageType == typeof(OrderPlaced) ? OrderPlaced.Descriptor : LegacyEvent.Descriptor;

        // Act & Assert
        ProtoSchemaWriter.SyntaxVersion(descriptor).ShouldBe(expected);
    }
}
