// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Bielu.AspNetCore.AsyncApi.Merger.Merge;
using Shouldly;
using Xunit;

namespace Bielu.AspNetCore.AsyncApi.Tests.Unit;

public class CachedAsyncApiMergeServiceTests : IDisposable
{
    private const string SourceUri = "https://orders.example.com/asyncapi/v1.json";

    private const string Document = """
        {
          "asyncapi": "3.0.0",
          "info": { "title": "Orders", "version": "1.0.0" },
          "channels": { "orderPlaced": { "address": "orders/placed" } }
        }
        """;

    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), $"asyncapi_merge_cache_{Guid.NewGuid():N}");

    public CachedAsyncApiMergeServiceTests() => Directory.CreateDirectory(_tempDir);

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    private static (CachedAsyncApiMergeService Service, StubHandler Handler) Create(TimeSpan cacheDuration, params string[] sources)
    {
        var handler = new StubHandler();
        var httpClient = new HttpClient(handler);
        var options = new AsyncApiMergeOptions { CacheDuration = cacheDuration };
        foreach (var source in sources.Length == 0 ? [SourceUri] : sources)
        {
            options.AddSource(source);
        }

        return (new CachedAsyncApiMergeService(new AsyncApiDocumentMerger(httpClient), options, httpClient), handler);
    }

    [Fact]
    public void Constructor_NullArguments_Throw()
    {
        var httpClient = new HttpClient();
        var merger = new AsyncApiDocumentMerger(httpClient);
        var options = new AsyncApiMergeOptions();

        Should.Throw<ArgumentNullException>(() => new CachedAsyncApiMergeService(null!, options, httpClient));
        Should.Throw<ArgumentNullException>(() => new CachedAsyncApiMergeService(merger, null!, httpClient));
        Should.Throw<ArgumentNullException>(() => new CachedAsyncApiMergeService(merger, options, null!));
    }

    [Fact]
    public async Task GetMergedDocumentAsync_WithinCacheDuration_ReturnsTheCachedDocumentWithoutRequests()
    {
        // Arrange
        var (service, handler) = Create(TimeSpan.FromHours(1));
        using var _ = service;

        // Act
        var first = await service.GetMergedDocumentAsync();
        var requestsAfterFirst = handler.Requests.Count;
        var second = await service.GetMergedDocumentAsync();

        // Assert
        first.Channels.ShouldContainKey("orderPlaced");
        second.ShouldBeSameAs(first);
        handler.Requests.Count.ShouldBe(requestsAfterFirst);
        handler.Requests.Count(r => r.Method == HttpMethod.Get).ShouldBe(1);
    }

    [Fact]
    public async Task GetMergedDocumentAsync_ExpiredButNotModified_RevalidatesWithEtagAndKeepsTheCache()
    {
        // Arrange
        var (service, handler) = Create(TimeSpan.Zero);
        using var _ = service;
        handler.HeadStatus = HttpStatusCode.NotModified;
        var first = await service.GetMergedDocumentAsync();

        // Act
        var second = await service.GetMergedDocumentAsync();

        // Assert
        second.ShouldBeSameAs(first);
        handler.Requests.Count(r => r.Method == HttpMethod.Get).ShouldBe(1);
        var revalidation = handler.Requests.Last(r => r.Method == HttpMethod.Head);
        revalidation.IfNoneMatch.ShouldBe("\"v1\"");
        revalidation.IfModifiedSince.ShouldBe(StubHandler.LastModified);
    }

    [Fact]
    public async Task GetMergedDocumentAsync_ExpiredAndModified_MergesAgain()
    {
        // Arrange
        var (service, handler) = Create(TimeSpan.Zero);
        using var _ = service;
        var first = await service.GetMergedDocumentAsync();

        // Act
        var second = await service.GetMergedDocumentAsync();

        // Assert
        second.ShouldNotBeSameAs(first);
        handler.Requests.Count(r => r.Method == HttpMethod.Get).ShouldBe(2);
    }

    [Fact]
    public async Task GetMergedDocumentAsync_ChangeCheckFails_AssumesChangedAndMergesAgain()
    {
        // Arrange
        var (service, handler) = Create(TimeSpan.Zero);
        using var _ = service;
        handler.FailHeadRequests = true;
        var first = await service.GetMergedDocumentAsync();

        // Act
        var second = await service.GetMergedDocumentAsync();

        // Assert
        second.ShouldNotBeSameAs(first);
        handler.Requests.Count(r => r.Method == HttpMethod.Get).ShouldBe(2);
    }

    [Fact]
    public async Task InvalidateCache_ForcesAMergeWithinTheCacheDuration()
    {
        // Arrange
        var (service, handler) = Create(TimeSpan.FromHours(1));
        using var _ = service;
        var first = await service.GetMergedDocumentAsync();

        // Act
        service.InvalidateCache();
        var second = await service.GetMergedDocumentAsync();

        // Assert
        second.ShouldNotBeSameAs(first);
        handler.Requests.Count(r => r.Method == HttpMethod.Get).ShouldBe(2);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GetMergedDocumentAsync_LocalFile_MergesAgainOnlyAfterItIsWritten(bool asFileUri)
    {
        // Arrange
        var path = Path.Combine(_tempDir, "orders.json");
        await File.WriteAllTextAsync(path, Document);
        File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddMinutes(-5));
        var (service, handler) = Create(TimeSpan.Zero, asFileUri ? new Uri(path).AbsoluteUri : path);
        using var _ = service;
        var first = await service.GetMergedDocumentAsync();

        // Act
        var unchanged = await service.GetMergedDocumentAsync();
        File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddMinutes(5));
        var changed = await service.GetMergedDocumentAsync();

        // Assert
        unchanged.ShouldBeSameAs(first);
        changed.ShouldNotBeSameAs(first);
        handler.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetMergedDocumentAsync_LocalFileThatDisappears_KeepsTheCache()
    {
        // Arrange
        var path = Path.Combine(_tempDir, "orders.json");
        await File.WriteAllTextAsync(path, Document);
        var (service, _) = Create(TimeSpan.Zero, path);
        using var __ = service;
        var first = await service.GetMergedDocumentAsync();

        // Act
        File.Delete(path);
        var second = await service.GetMergedDocumentAsync();

        // Assert
        second.ShouldBeSameAs(first);
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        public static readonly DateTimeOffset LastModified = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        public List<(HttpMethod Method, string? IfNoneMatch, DateTimeOffset? IfModifiedSince)> Requests { get; } = [];

        public HttpStatusCode HeadStatus { get; set; } = HttpStatusCode.OK;

        public bool FailHeadRequests { get; set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add((request.Method, request.Headers.IfNoneMatch.FirstOrDefault()?.Tag, request.Headers.IfModifiedSince));

            if (request.Method == HttpMethod.Head && FailHeadRequests)
            {
                throw new HttpRequestException("HEAD not allowed");
            }

            var response = new HttpResponseMessage(request.Method == HttpMethod.Head ? HeadStatus : HttpStatusCode.OK)
            {
                Content = new StringContent(request.Method == HttpMethod.Get ? Document : string.Empty, Encoding.UTF8, "application/json"),
            };
            response.Headers.ETag = new EntityTagHeaderValue("\"v1\"");
            response.Content.Headers.LastModified = LastModified;
            return Task.FromResult(response);
        }
    }
}
