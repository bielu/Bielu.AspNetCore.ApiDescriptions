// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Bielu.AspNetCore.AsyncApi.Cli.Commands;
using Bielu.Cli.Shared.Diff;
using ByteBard.AsyncAPI.Models;
using Shouldly;
using Xunit;

namespace Bielu.AspNetCore.AsyncApi.Cli.Tests;

public class AsyncApiDocumentComparerTests
{
    private static AsyncApiDocument Document() => new()
    {
        Info = new AsyncApiInfo { Title = "API", Version = "1.0.0" },
    };

    private static List<DocumentChange> Compare(AsyncApiDocument @base, AsyncApiDocument head) =>
        new AsyncApiDocumentComparer().Compare(@base, head).ToList();

    [Fact]
    public void Compare_IdenticalDocuments_ReportsNothing()
    {
        // Arrange
        var document = Document();
        document.Channels["users"] = new AsyncApiChannel { Address = "users" };

        // Act
        var changes = Compare(document, document);

        // Assert
        changes.ShouldBeEmpty();
    }

    [Fact]
    public void Compare_ServerRemovedAndAdded_ReportsBreakingAndNonBreaking()
    {
        // Arrange
        var @base = Document();
        @base.Servers["old"] = new AsyncApiServer { Host = "old", Protocol = "kafka" };
        var head = Document();
        head.Servers["new"] = new AsyncApiServer { Host = "new", Protocol = "kafka" };

        // Act
        var changes = Compare(@base, head);

        // Assert
        changes.ShouldContain(c => c.Path == "servers/old" && c.Severity == ChangeSeverity.Breaking);
        changes.ShouldContain(c => c.Path == "servers/new" && c.Severity == ChangeSeverity.NonBreaking);
    }

    [Fact]
    public void Compare_ChannelRemovedAddedAndReaddressed_ReportsEach()
    {
        // Arrange
        var @base = Document();
        @base.Channels["removed"] = new AsyncApiChannel { Address = "removed" };
        @base.Channels["moved"] = new AsyncApiChannel { Address = "v1/moved" };
        var head = Document();
        head.Channels["moved"] = new AsyncApiChannel { Address = "v2/moved" };
        head.Channels["added"] = new AsyncApiChannel { Address = "added" };

        // Act
        var changes = Compare(@base, head);

        // Assert
        changes.Count.ShouldBe(3);
        changes.ShouldContain(c => c.Path == "channels/removed" && c.Severity == ChangeSeverity.Breaking);
        changes.ShouldContain(c => c.Path == "channels/moved/address" && c.Severity == ChangeSeverity.Breaking);
        changes.ShouldContain(c => c.Path == "channels/added" && c.Severity == ChangeSeverity.NonBreaking);
    }

    [Fact]
    public void Compare_OperationActionAndChannelChanged_ReportsBothAsBreaking()
    {
        // Arrange
        var @base = Document();
        @base.Operations["onUser"] = new AsyncApiOperation
        {
            Action = AsyncApiAction.Receive,
            Channel = new AsyncApiChannelReference("#/channels/users"),
        };
        var head = Document();
        head.Operations["onUser"] = new AsyncApiOperation
        {
            Action = AsyncApiAction.Send,
            Channel = new AsyncApiChannelReference("#/channels/members"),
        };

        // Act
        var changes = Compare(@base, head);

        // Assert
        changes.ShouldContain(c => c.Path == "operations/onUser/action" && c.Severity == ChangeSeverity.Breaking);
        changes.ShouldContain(c => c.Path == "operations/onUser/channel" && c.Severity == ChangeSeverity.Breaking);
    }

    [Fact]
    public void Compare_OperationRemovedAndAdded_ReportsEach()
    {
        // Arrange
        var @base = Document();
        @base.Operations["old"] = new AsyncApiOperation { Action = AsyncApiAction.Send };
        var head = Document();
        head.Operations["new"] = new AsyncApiOperation { Action = AsyncApiAction.Send };

        // Act
        var changes = Compare(@base, head);

        // Assert
        changes.ShouldContain(c => c.Path == "operations/old" && c.Severity == ChangeSeverity.Breaking);
        changes.ShouldContain(c => c.Path == "operations/new" && c.Severity == ChangeSeverity.NonBreaking);
    }

    [Fact]
    public void Compare_ComponentMessagesAndSchemasChanged_ReportsEach()
    {
        // Arrange
        var @base = Document();
        @base.Components = new AsyncApiComponents();
        @base.Components.Messages["Removed"] = new AsyncApiMessage();
        @base.Components.Schemas["OldSchema"] = new AsyncApiMultiFormatSchema { Schema = new AsyncApiJsonSchema() };
        var head = Document();
        head.Components = new AsyncApiComponents();
        head.Components.Messages["Added"] = new AsyncApiMessage();
        head.Components.Schemas["NewSchema"] = new AsyncApiMultiFormatSchema { Schema = new AsyncApiJsonSchema() };

        // Act
        var changes = Compare(@base, head);

        // Assert
        changes.ShouldContain(c => c.Path == "components/messages/Removed" && c.Severity == ChangeSeverity.Breaking);
        changes.ShouldContain(c => c.Path == "components/messages/Added" && c.Severity == ChangeSeverity.NonBreaking);
        changes.ShouldContain(c => c.Path == "components/schemas/OldSchema" && c.Severity == ChangeSeverity.Breaking);
        changes.ShouldContain(c => c.Path == "components/schemas/NewSchema" && c.Severity == ChangeSeverity.NonBreaking);
    }

    [Fact]
    public void Compare_ComponentsOnlyOnOneSide_TreatsTheOtherAsEmpty()
    {
        // Arrange
        var @base = Document();
        @base.Components = null;
        var head = Document();
        head.Components = new AsyncApiComponents();
        head.Components.Messages["Added"] = new AsyncApiMessage();

        // Act
        var added = Compare(@base, head);
        var removed = Compare(head, @base);

        // Assert
        added.ShouldHaveSingleItem().Severity.ShouldBe(ChangeSeverity.NonBreaking);
        removed.ShouldHaveSingleItem().Severity.ShouldBe(ChangeSeverity.Breaking);
    }
}
