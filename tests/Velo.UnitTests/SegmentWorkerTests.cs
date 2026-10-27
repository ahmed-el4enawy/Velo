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
    public async Task SegmentWorker_WhenServerReturns200OK_ShouldThrowRangeNotSupportedException()
    {
        var mockHandler = new MockHttpMessageHandler(req =>
        {
            var res = new HttpResponseMessage(HttpStatusCode.OK);
            res.Content = new ByteArrayContent(Encoding.UTF8.GetBytes("FULL_BODY_INSTEAD_OF_206"));
            return res;
        });

        var httpClient = new HttpClient(mockHandler);
        var fileManager = new PositionalFileManager();
        string tempPath = Path.Combine(_testDir, "test_200.velo.part");
        await fileManager.PrepareStagingFileAsync(tempPath, 100);

        var job = new DownloadJob
        {
            EffectiveUrl = "https://example.com/test",
            TempPath = tempPath,
            ExpectedBytes = 100
        };

        var segment = new DownloadSegment
        {
            StartByte = 0,
            EndByte = 99,
            NextByte = 0
        };

        var worker = new SegmentWorker(httpClient, fileManager);

        var act = async () => await worker.DownloadSegmentAsync(job, segment);
        await act.Should().ThrowAsync<RangeNotSupportedException>();
    }

    [Fact]
    public async Task SegmentWorker_WhenServerReturnsMismatchedContentRange_ShouldThrowInvalidOperationException()
    {
        var mockHandler = new MockHttpMessageHandler(req =>
        {
            var res = new HttpResponseMessage(HttpStatusCode.PartialContent);
            res.Content = new ByteArrayContent(new byte[50]);
            // Return range starting at 10 instead of requested 0
            res.Content.Headers.ContentRange = new System.Net.Http.Headers.ContentRangeHeaderValue(10, 59, 100);
            return res;
        });

        var httpClient = new HttpClient(mockHandler);
        var fileManager = new PositionalFileManager();
        string tempPath = Path.Combine(_testDir, "test_mismatch.velo.part");
        await fileManager.PrepareStagingFileAsync(tempPath, 100);

        var job = new DownloadJob
        {
            EffectiveUrl = "https://example.com/test",
            TempPath = tempPath,
            ExpectedBytes = 100
        };

        var segment = new DownloadSegment
        {
            StartByte = 0,
            EndByte = 49,
            NextByte = 0
        };

        var worker = new SegmentWorker(httpClient, fileManager);

        var act = async () => await worker.DownloadSegmentAsync(job, segment);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*mismatched range offset*");
    }

    [Fact]
    public async Task SegmentWorker_WhenTransientConnectionDropOccurs_ShouldRetryAndCompleteBytePerfect()
    {
        byte[] completeData = Encoding.UTF8.GetBytes("CHUNK_ONE_DATA_PLUS_CHUNK_TWO_AFTER_RETRY");
        int attempt = 0;

        var mockHandler = new MockHttpMessageHandler(req =>
        {
            attempt++;
            long from = req.Headers.Range!.Ranges.First().From!.Value;
            long to = req.Headers.Range!.Ranges.First().To!.Value;

            if (attempt == 1)
            {
                // First attempt: simulate socket drop after reading 10 bytes
                byte[] partialChunk = new byte[10];
                Array.Copy(completeData, from, partialChunk, 0, 10);

                var res = new HttpResponseMessage(HttpStatusCode.PartialContent);
                res.Content = new ByteArrayContent(partialChunk);
                res.Content.Headers.ContentRange = new System.Net.Http.Headers.ContentRangeHeaderValue(from, to, completeData.Length);
                return res;
            }
            else
            {
                // Subsequent attempt: stream remainder
                int remaining = (int)(to - from + 1);
                byte[] rest = new byte[remaining];
                Array.Copy(completeData, from, rest, 0, remaining);

                var res = new HttpResponseMessage(HttpStatusCode.PartialContent);
                res.Content = new ByteArrayContent(rest);
                res.Content.Headers.ContentRange = new System.Net.Http.Headers.ContentRangeHeaderValue(from, to, completeData.Length);
                return res;
            }
        });

        var httpClient = new HttpClient(mockHandler);
        var fileManager = new PositionalFileManager();
        string tempPath = Path.Combine(_testDir, "test_retry.velo.part");
        await fileManager.PrepareStagingFileAsync(tempPath, completeData.Length);

        var job = new DownloadJob
        {
            EffectiveUrl = "https://example.com/test",
            TempPath = tempPath,
            ExpectedBytes = completeData.Length
        };

        var segment = new DownloadSegment
        {
            StartByte = 0,
            EndByte = completeData.Length - 1,
            NextByte = 0
        };

        var worker = new SegmentWorker(httpClient, fileManager);
        await worker.DownloadSegmentAsync(job, segment);

        segment.State.Should().Be(SegmentState.Completed);
        attempt.Should().BeGreaterThan(1, "Should have performed at least one retry");

        byte[] written = await File.ReadAllBytesAsync(tempPath);
        written.Should().BeEquivalentTo(completeData);
    }

    [Fact]
    public async Task MultiSegmentEngine_WhenServerRefusesRangeDuringSegmentedDownload_ShouldFallbackToSequentialAndSucceed()
    {
        byte[] expectedPayload = Encoding.UTF8.GetBytes("ACCELERATED_PAYLOAD_WITH_DYNAMIC_FALLBACK");
        var mockHandler = new MockHttpMessageHandler(req =>
        {
            // Server refuses range header and returns 200 OK
            var res = new HttpResponseMessage(HttpStatusCode.OK);
            res.Content = new ByteArrayContent(expectedPayload);
            res.Content.Headers.ContentLength = expectedPayload.Length;
            return res;
        });

        var httpClient = new HttpClient(mockHandler);
        var fileManager = new PositionalFileManager();
        var engine = new MultiSegmentEngine(httpClient, fileManager);

        string tempPath = Path.Combine(_testDir, "fallback_dynamic.velo.part");
        string finalPath = Path.Combine(_testDir, "fallback_dynamic.bin");

        var job = new DownloadJob
        {
            EffectiveUrl = "https://example.com/file.bin",
            TempPath = tempPath,
            TargetPath = finalPath,
            RangeSupported = true, // Probe originally said true, but runtime server refused range
            ExpectedBytes = expectedPayload.Length
        };

        await engine.ExecuteDownloadAsync(job);

        job.State.Should().Be(JobState.Completed);
        job.RangeSupported.Should().BeFalse("Engine should have demoted RangeSupported upon 200 OK fallback");
        File.Exists(finalPath).Should().BeTrue();
        (await File.ReadAllBytesAsync(finalPath)).Should().BeEquivalentTo(expectedPayload);
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
