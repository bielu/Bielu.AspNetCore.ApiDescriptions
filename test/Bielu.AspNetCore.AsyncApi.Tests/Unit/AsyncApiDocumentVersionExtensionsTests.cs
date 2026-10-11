// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Bielu.AspNetCore.AsyncApi.Extensions;
using ByteBard.AsyncAPI;
using ByteBard.AsyncAPI.Models;
using Shouldly;
using Xunit;

namespace Bielu.AspNetCore.AsyncApi.Tests.Unit;

/// <summary>
/// Unit tests for <see cref="AsyncApiDocumentVersionExtensions"/>.
/// </summary>
public class AsyncApiDocumentVersionExtensionsTests
{
    [Theory]
    [InlineData("2.0.0", AsyncApiVersion.AsyncApi2_0, true)]
    [InlineData("2.6.0", AsyncApiVersion.AsyncApi2_0, true)]
    [InlineData("2.6.0", AsyncApiVersion.AsyncApi3_0, false)]
    [InlineData("3.0.0", AsyncApiVersion.AsyncApi3_0, true)]
    [InlineData("3.1.0", AsyncApiVersion.AsyncApi3_0, true)]
    [InlineData("3.1.0", AsyncApiVersion.AsyncApi2_0, false)]
    [InlineData("12.0.0", AsyncApiVersion.AsyncApi2_0, false)]
    [InlineData("23.0.0", AsyncApiVersion.AsyncApi2_0, false)]
    [InlineData("", AsyncApiVersion.AsyncApi2_0, false)]
    [InlineData("", AsyncApiVersion.AsyncApi3_0, false)]
    [InlineData(null, AsyncApiVersion.AsyncApi2_0, false)]
    [InlineData(null, AsyncApiVersion.AsyncApi3_0, false)]
    public void IsAsyncApiVersion_ForDeclaredVersion_ReturnsWhetherMajorVersionMatches(
        string? declaredVersion, AsyncApiVersion version, bool expected)
    {
        // Arrange
        var document = new AsyncApiDocument { Asyncapi = declaredVersion! };

        // Act
        var result = document.IsAsyncApiVersion(version);

        // Assert
        result.ShouldBe(expected);
    }

    [Fact]
    public void IsAsyncApiVersion_ThrowsForNullDocument()
    {
        // Arrange
        AsyncApiDocument document = null!;

        // Act & Assert
        Should.Throw<ArgumentNullException>(() => document.IsAsyncApiVersion(AsyncApiVersion.AsyncApi2_0));
    }

    [Fact]
    public void IsAsyncApiVersion_ThrowsForUnknownVersion()
    {
        // Arrange
        var document = new AsyncApiDocument { Asyncapi = "3.1.0" };

        // Act & Assert
        Should.Throw<ArgumentOutOfRangeException>(() => document.IsAsyncApiVersion((AsyncApiVersion)999));
    }
}
