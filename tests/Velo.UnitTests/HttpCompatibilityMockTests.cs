using System.Net;
using System.Text;
using FluentAssertions;
using Velo.Domain;
using Velo.DownloadEngine;
using Xunit;

namespace Velo.UnitTests;

public class HttpCompatibilityMockTests
{
    private sealed class DelegatingHandlerStub(Func<HttpRequestMessage, HttpResponseMessage> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(handler(request));
    }

    [Fact]
    public async Task HttpProbe_DecodesRfc5987ContentDispositionFileName()
    {
        var stub = new DelegatingHandlerStub(req =>
        {
            var res = new HttpResponseMessage(HttpStatusCode.OK);
            res.Headers.Add("Accept-Ranges", "bytes");
            res.Content = new ByteArrayContent(Encoding.UTF8.GetBytes("Data"));
            res.Content.Headers.ContentLength = 4;
            res.Content.Headers.Add("Content-Disposition", "attachment; filename*=UTF-8''Special%20Archive%202026.zip");
            return res;
        });

        using var client = new HttpClient(stub);
        var probe = new HttpProbe(client);

        var result = await probe.ProbeUrlAsync("https://example.com/download");

        result.IsSuccess.Should().BeTrue();
        result.ContentDispositionFileName.Should().Be("Special Archive 2026.zip");
        result.RangeSupported.Should().BeTrue();
    }

    [Fact]
    public async Task HttpProbe_DecodesStandardContentDispositionFileName()
    {
        var stub = new DelegatingHandlerStub(req =>
        {
            var res = new HttpResponseMessage(HttpStatusCode.OK);
            res.Headers.Add("Accept-Ranges", "bytes");
            res.Content = new ByteArrayContent(Encoding.UTF8.GetBytes("Data"));
            res.Content.Headers.ContentLength = 4;
            res.Content.Headers.Add("Content-Disposition", "attachment; filename=\"custom-archive-file.tar.gz\"");
            return res;
        });

        using var client = new HttpClient(stub);
        var probe = new HttpProbe(client);

        var result = await probe.ProbeUrlAsync("https://example.com/file");

        result.IsSuccess.Should().BeTrue();
        result.ContentDispositionFileName.Should().Be("custom-archive-file.tar.gz");
    }

    [Fact]
    public async Task HttpProbe_WhenRangesIgnored_ReportsRangeNotSupported()
    {
        var stub = new DelegatingHandlerStub(req =>
        {
            // Range probe returns 200 OK instead of 206
            var res = new HttpResponseMessage(HttpStatusCode.OK);
            res.Content = new ByteArrayContent(Encoding.UTF8.GetBytes("FullPayload"));
            res.Content.Headers.ContentLength = 11;
            return res;
        });

        using var client = new HttpClient(stub);
        var probe = new HttpProbe(client);

        var result = await probe.ProbeUrlAsync("https://example.com/stream");

        result.IsSuccess.Should().BeTrue();
        result.RangeSupported.Should().BeFalse();
        result.ContentLength.Should().Be(11);
    }
}
