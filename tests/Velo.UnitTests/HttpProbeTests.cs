using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;
using Velo.DownloadEngine;
using Xunit;

namespace Velo.UnitTests;

public class HttpProbeTests
{
    private sealed class MockHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(handler(request));
    }

    [Theory]
    [InlineData("ftp://example.com/file.iso")]
    [InlineData("file:///C:/path/file.txt")]
    [InlineData("javascript:alert(1)")]
    [InlineData("invalid-url")]
    public async Task ProbeUrlAsync_InvalidScheme_ShouldFailImmediately(string url)
    {
        var probe = new HttpProbe();
        var result = await probe.ProbeUrlAsync(url);

        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Invalid or unsupported URL scheme");
    }

    [Fact]
    public async Task ProbeUrlAsync_HeadWithRangeSupport_ShouldSucceed()
    {
        var mockHandler = new MockHttpMessageHandler(req =>
        {
            var res = new HttpResponseMessage(HttpStatusCode.OK);
            res.Content = new ByteArrayContent([]);
            res.Content.Headers.ContentLength = 10485760; // 10 MB
            res.Content.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment")
            {
                FileName = "test_setup.exe"
            };
            res.Headers.AcceptRanges.Add("bytes");
            res.Headers.ETag = new EntityTagHeaderValue("\"test-etag-123\"");
            return res;
        });

        var httpClient = new HttpClient(mockHandler);
        var probe = new HttpProbe(httpClient);

        var result = await probe.ProbeUrlAsync("https://example.com/download");

        result.IsSuccess.Should().BeTrue();
        result.RangeSupported.Should().BeTrue();
        result.ContentLength.Should().Be(10485760);
        result.ContentDispositionFileName.Should().Be("test_setup.exe");
        result.ETag.Should().Be("\"test-etag-123\"");
    }

    [Fact]
    public async Task ProbeUrlAsync_Head405_ShouldFallbackToGetRange()
    {
        var mockHandler = new MockHttpMessageHandler(req =>
        {
            if (req.Method == HttpMethod.Head)
            {
                return new HttpResponseMessage(HttpStatusCode.MethodNotAllowed);
            }

            // Fallback GET Range: bytes=0-0
            var res = new HttpResponseMessage(HttpStatusCode.PartialContent);
            res.Content = new ByteArrayContent([0x4D]);
            res.Content.Headers.ContentRange = new ContentRangeHeaderValue(0, 0, 52428800); // 50 MB
            res.Content.Headers.ContentLength = 1;
            return res;
        });

        var httpClient = new HttpClient(mockHandler);
        var probe = new HttpProbe(httpClient);

        var result = await probe.ProbeUrlAsync("https://example.com/stream/file.bin");

        result.IsSuccess.Should().BeTrue();
        result.RangeSupported.Should().BeTrue();
        result.ContentLength.Should().Be(52428800);
        result.ContentDispositionFileName.Should().Be("file.bin");
    }

    [Fact]
    public async Task ProbeUrlAsync_ServerReturns200ToRange_ShouldDisableRangeSupport()
    {
        var mockHandler = new MockHttpMessageHandler(req =>
        {
            if (req.Method == HttpMethod.Head)
            {
                return new HttpResponseMessage(HttpStatusCode.NotFound);
            }

            // Server ignores Range header and returns full content 200 OK
            var res = new HttpResponseMessage(HttpStatusCode.OK);
            res.Content = new ByteArrayContent(new byte[1024]);
            res.Content.Headers.ContentLength = 1024;
            return res;
        });

        var httpClient = new HttpClient(mockHandler);
        var probe = new HttpProbe(httpClient);

        var result = await probe.ProbeUrlAsync("https://example.com/file.zip");

        result.IsSuccess.Should().BeTrue();
        result.RangeSupported.Should().BeFalse("Server returned 200 OK instead of 206 Partial Content");
        result.ContentLength.Should().Be(1024);
    }
}
