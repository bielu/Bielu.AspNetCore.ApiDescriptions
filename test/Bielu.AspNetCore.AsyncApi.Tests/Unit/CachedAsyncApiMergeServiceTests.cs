using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Bielu.AspNetCore.AsyncApi.Merger.Merge;
using Shouldly;
using Xunit;

namespace Bielu.AspNetCore.AsyncApi.Tests.Unit;

/// <summary>
/// Unit tests for <see cref="CachedAsyncApiMergeService"/>: when the cached merge is reused, and how remote (ETag /
/// Last-Modified) and local (last write time) sources are checked for changes once the cache duration elapses.
/// </summary>
public sealed class CachedAsyncApiMergeServiceTests : IDisposable
{
    private const string RemoteUri = "https://orders.example.com/asyncapi.json";

    private const string Document = """
        { "asyncapi": "3.0.0", "info": { "title": "Orders", "version": "1.0.0" }, "channels": { "orders": { "address": "orders" } } }
        """;

    private readonly string _directory = Path.Combine(Path.GetTempPath(), "asyncapi-merge-cache-tests", Guid.NewGuid().ToString("N"));

    public CachedAsyncApiMergeServiceTests() => Directory.CreateDirectory(_directory);

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    /// <summary>
    /// Serves the document on GET, and answers HEAD with an ETag and Last-Modified, or 304 when the request's
    /// validators still match.
    /// </summary>
    private sealed class FakeSource : HttpMessageHandler
    {
        public string ETag { get; set; } = "\"v1\"";

        public bool FailHead { get; set; }

        public int Gets { get; private set; }

        public List<HttpRequestMessage> Heads { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Method == HttpMethod.Get)
            {
                Gets++;
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(Document, Encoding.UTF8, "application/json"),
                });
            }

            Heads.Add(request);
            if (FailHead)
            {
                throw new HttpRequestException("unreachable");
            }

            if (request.Headers.IfNoneMatch.Any(tag => tag.Tag == ETag))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotModified));
            }

            var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([]) };
            response.Headers.ETag = new EntityTagHeaderValue(ETag);
            response.Content.Headers.LastModified = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
            return Task.FromResult(response);
        }
    }

    private static CachedAsyncApiMergeService CreateService(FakeSource source, AsyncApiMergeOptions options)
    {
        var httpClient = new HttpClient(source);
        return new CachedAsyncApiMergeService(new AsyncApiDocumentMerger(httpClient), options, httpClient);
    }

    [Fact]
    public async Task GetMergedDocumentAsync_WithinCacheDuration_ReusesTheMerge()
    {
        // Arrange
        var source = new FakeSource();
        using var service = CreateService(source, new AsyncApiMergeOptions().AddSource(RemoteUri));

        // Act
        var first = await service.GetMergedDocumentAsync();
        var second = await service.GetMergedDocumentAsync();

        // Assert
        second.ShouldBeSameAs(first);
        source.Gets.ShouldBe(1);
        first.Channels.ShouldContainKey("orders");
    }

    [Fact]
    public async Task GetMergedDocumentAsync_RemoteSourceNotModified_ReusesTheMerge()
    {
        // Arrange
        var source = new FakeSource();
        var options = new AsyncApiMergeOptions { CacheDuration = TimeSpan.Zero }.AddSource(RemoteUri);
        using var service = CreateService(source, options);

        // Act
        var first = await service.GetMergedDocumentAsync();
        var second = await service.GetMergedDocumentAsync();

        // Assert: the change check sent the remembered ETag and Last-Modified, and got 304.
        second.ShouldBeSameAs(first);
        var check = source.Heads.Last();
        check.Headers.IfNoneMatch.ShouldContain(tag => tag.Tag == "\"v1\"");
        check.Headers.IfModifiedSince.ShouldNotBeNull();
    }

    [Fact]
    public async Task GetMergedDocumentAsync_RemoteSourceChanged_MergesAgain()
    {
        // Arrange
        var source = new FakeSource();
        var options = new AsyncApiMergeOptions { CacheDuration = TimeSpan.Zero }.AddSource(RemoteUri);
        using var service = CreateService(source, options);
        var first = await service.GetMergedDocumentAsync();
        source.ETag = "\"v2\"";

        // Act
        var second = await service.GetMergedDocumentAsync();

        // Assert
        second.ShouldNotBeSameAs(first);
        source.Gets.ShouldBe(2);
    }

    [Fact]
    public async Task GetMergedDocumentAsync_ChangeCheckFails_AssumesChangedAndMergesAgain()
    {
        // Arrange
        var source = new FakeSource();
        var options = new AsyncApiMergeOptions { CacheDuration = TimeSpan.Zero }.AddSource(RemoteUri);
        using var service = CreateService(source, options);
        var first = await service.GetMergedDocumentAsync();
        source.FailHead = true;

        // Act
        var second = await service.GetMergedDocumentAsync();

        // Assert
        second.ShouldNotBeSameAs(first);
    }

    [Fact]
    public async Task GetMergedDocumentAsync_LocalFileRewritten_MergesAgain()
    {
        // Arrange
        var path = Path.Combine(_directory, "orders.json");
        await File.WriteAllTextAsync(path, Document);
        var options = new AsyncApiMergeOptions { CacheDuration = TimeSpan.Zero }.AddSource(new Uri(path).AbsoluteUri);
        using var service = CreateService(new FakeSource(), options);
        var first = await service.GetMergedDocumentAsync();

        // Act: unchanged, then rewritten after the merge.
        var unchanged = await service.GetMergedDocumentAsync();
        File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddMinutes(1));
        var rewritten = await service.GetMergedDocumentAsync();

        // Assert
        unchanged.ShouldBeSameAs(first);
        rewritten.ShouldNotBeSameAs(first);
    }

    [Fact]
    public async Task GetMergedDocumentAsync_MissingLocalFile_IsNotTreatedAsAChange()
    {
        // Arrange
        var path = Path.Combine(_directory, "gone.json");
        await File.WriteAllTextAsync(path, Document);
        var options = new AsyncApiMergeOptions { CacheDuration = TimeSpan.Zero }.AddSource(path);
        using var service = CreateService(new FakeSource(), options);
        var first = await service.GetMergedDocumentAsync();
        File.Delete(path);

        // Act
        var second = await service.GetMergedDocumentAsync();

        // Assert
        second.ShouldBeSameAs(first);
    }

    [Fact]
    public async Task InvalidateCache_ForcesTheNextRequestToMergeAgain()
    {
        // Arrange
        var source = new FakeSource();
        using var service = CreateService(source, new AsyncApiMergeOptions().AddSource(RemoteUri));
        var first = await service.GetMergedDocumentAsync();

        // Act
        service.InvalidateCache();
        var second = await service.GetMergedDocumentAsync();

        // Assert
        second.ShouldNotBeSameAs(first);
        source.Gets.ShouldBe(2);
    }

    [Fact]
    public void Constructor_NullArguments_Throw()
    {
        // Arrange
        using var httpClient = new HttpClient();
        var merger = new AsyncApiDocumentMerger(httpClient);
        var options = new AsyncApiMergeOptions();

        // Act & Assert
        Should.Throw<ArgumentNullException>(() => new CachedAsyncApiMergeService(null!, options, httpClient));
        Should.Throw<ArgumentNullException>(() => new CachedAsyncApiMergeService(merger, null!, httpClient));
        Should.Throw<ArgumentNullException>(() => new CachedAsyncApiMergeService(merger, options, null!));
    }
}
