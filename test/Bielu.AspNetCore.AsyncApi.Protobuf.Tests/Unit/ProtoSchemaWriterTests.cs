using Bielu.AspNetCore.AsyncApi.Protobuf.Tests.Catalog;
using Bielu.AspNetCore.AsyncApi.Protobuf.Tests.Legacy;
using Bielu.AspNetCore.AsyncApi.Protobuf.Tests.Mixed;
using Bielu.AspNetCore.AsyncApi.Protobuf.Tests.Modern;
using Bielu.AspNetCore.AsyncApi.Protobuf.Tests.Orders;
using Google.Protobuf.Reflection;
using Shouldly;
using Xunit;

namespace Bielu.AspNetCore.AsyncApi.Protobuf.Tests.Unit;

public class ProtoSchemaWriterTests
{
    [Fact]
    public void TryWrite_Proto3Message_RendersTheWireContract()
    {
        // Act
        var source = Write(OrderPlaced.Descriptor);

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
    public void TryWrite_LeavesOutTypesTheMessageDoesNotUse()
    {
        // Act & Assert
        Write(OrderPlaced.Descriptor).ShouldNotContain("Unrelated");
    }

    [Fact]
    public void TryWrite_Proto2Message_KeepsItsLabels()
    {
        // Act
        var source = Write(LegacyEvent.Descriptor);

        // Assert
        source.ShouldStartWith("syntax = \"proto2\";");
        source.ShouldContain("  required string id = 1;\n");
        source.ShouldContain("  optional int32 count = 2;\n");
        source.ShouldContain("  repeated string tags = 3;\n");
    }

    [Fact]
    public void TryWrite_Proto2Message_KeepsItsDefaults()
    {
        // Act
        var source = Write(LegacyEvent.Descriptor);

        // Assert
        source.ShouldContain("  optional int32 retries = 4 [default = 3];\n");
        source.ShouldContain("  optional string label = 5 [default = \"say \\\"hi\\\"\\n\"];\n");
        source.ShouldContain("  optional bytes magic = 6 [default = \"\\001\\002\"];\n");
        source.ShouldContain("  optional Level level = 7 [default = HIGH];\n");
    }

    [Fact]
    public void TryWrite_NestedMessage_MarksItAndDeclaresItsParent()
    {
        // Act
        var source = Write(OrderPlaced.Types.Meta.Descriptor);

        // Assert
        source.ShouldContain("message OrderPlaced {");
        source.ShouldContain("  // @RootNode\n  message Meta {");
    }

    [Fact]
    public void TryWrite_TypesSharingAName_RenamesTheOneFromAnotherPackage()
    {
        // Act
        var source = Write(Listing.Descriptor);

        // Assert
        source.ShouldBe("""
            syntax = "proto3";

            package test.catalog;

            // @RootNode
            message Listing {
              Item item = 1;
              test_inventory_Item stock = 2;
              test_inventory_Item.Batch batch = 3;
            }

            message Item {
              string name = 1;
            }

            message test_inventory_Item {
              message Batch {
                int32 number = 1;
              }
              int32 count = 1;
            }

            """.ReplaceLineEndings("\n"));
    }

    [Fact]
    public void TryWrite_MessageUsingTypesOfAnotherSyntax_IsNotWritten()
    {
        // Act & Assert
        ProtoSchemaWriter.TryWrite(Wrapper.Descriptor, out var source).ShouldBeFalse();
        source.ShouldBeNull();
    }

    [Fact]
    public void TryWrite_EditionsMessage_IsNotWritten()
    {
        // Act & Assert
        ProtoSchemaWriter.TryWrite(ModernEvent.Descriptor, out var source).ShouldBeFalse();
        source.ShouldBeNull();
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

    private static string Write(MessageDescriptor descriptor)
    {
        ProtoSchemaWriter.TryWrite(descriptor, out var source).ShouldBeTrue();
        return source!;
    }
}
