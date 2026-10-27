using System.Net;
using System.Text;
using FluentAssertions;
using Velo.Domain;
using Velo.DownloadEngine;
using Xunit;

namespace Velo.UnitTests;

public class SegmentWorkerTests : IDisposable
{
    private readonly string _testDir;

    public SegmentWorkerTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "Velo_SegmentTests_" + Guid.NewGuid());
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
        {
            try { Directory.Delete(_testDir, true); } catch { /* Ignore cleanup errors */ }
        }
    }

    private sealed class MockHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(handler(request));
    }

    [Fact]
    public async Task DownloadSegmentAsync_ValidRange_ShouldWriteToStagingFile()
    {
        byte[] sourceData = Encoding.UTF8.GetBytes("HELLO_VELO_MULTI_THREADED_ACCELERATION");
        var mockHandler = new MockHttpMessageHandler(req =>
        {
            req.Headers.Range.Should().NotBeNull();
            long from = req.Headers.Range!.Ranges.First().From!.Value;
            long to = req.Headers.Range!.Ranges.First().To!.Value;

            int count = (int)(to - from + 1);
            byte[] slice = new byte[count];
            Array.Copy(sourceData, from, slice, 0, count);

            var res = new HttpResponseMessage(HttpStatusCode.PartialContent);
            res.Content = new ByteArrayContent(slice);
            return res;
        });

        var httpClient = new HttpClient(mockHandler);
        var fileManager = new PositionalFileManager();
        string tempPath = Path.Combine(_testDir, "test.velo.part");

        await fileManager.PrepareStagingFileAsync(tempPath, sourceData.Length);

        var job = new DownloadJob
        {
            EffectiveUrl = "https://example.com/data.bin",
            TempPath = tempPath,
            ExpectedBytes = sourceData.Length
        };

        var segment = new DownloadSegment
        {
            StartByte = 0,
            EndByte = sourceData.Length - 1,
            NextByte = 0
        };

        var worker = new SegmentWorker(httpClient, fileManager);
        await worker.DownloadSegmentAsync(job, segment);

        segment.State.Should().Be(SegmentState.Completed);
        segment.IsCompleted.Should().BeTrue();

        byte[] written = await File.ReadAllBytesAsync(tempPath);
        written.Should().BeEquivalentTo(sourceData);
    }

    [Fact]
    public async Task MultiSegmentEngine_SequentialFallback_ShouldDownloadAndFinalize()
    {
        byte[] expectedPayload = Encoding.UTF8.GetBytes("SEQUENTIAL_FALLBACK_STREAM_DATA");
        var mockHandler = new MockHttpMessageHandler(req =>
        {
            var res = new HttpResponseMessage(HttpStatusCode.OK);
            res.Content = new ByteArrayContent(expectedPayload);
            return res;
        });

        var httpClient = new HttpClient(mockHandler);
        var fileManager = new PositionalFileManager();
        var engine = new MultiSegmentEngine(httpClient, fileManager);

        string tempPath = Path.Combine(_testDir, "stream.velo.part");
        string finalPath = Path.Combine(_testDir, "stream.txt");

        var job = new DownloadJob
        {
            EffectiveUrl = "https://example.com/stream",
            TempPath = tempPath,
            TargetPath = finalPath,
            RangeSupported = false,
            ExpectedBytes = expectedPayload.Length
        };

        await engine.ExecuteDownloadAsync(job);

        job.State.Should().Be(JobState.Completed);
        File.Exists(finalPath).Should().BeTrue();
        (await File.ReadAllBytesAsync(finalPath)).Should().BeEquivalentTo(expectedPayload);
    }
}
