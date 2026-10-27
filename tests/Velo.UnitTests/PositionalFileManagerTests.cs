using System.Text;
using FluentAssertions;
using Velo.DownloadEngine;
using Xunit;

namespace Velo.UnitTests;

public class PositionalFileManagerTests : IDisposable
{
    private readonly string _testDirectory;

    public PositionalFileManagerTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), "Velo_UnitTests_" + Guid.NewGuid());
        Directory.CreateDirectory(_testDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            try { Directory.Delete(_testDirectory, true); } catch { /* Ignore temp cleanup */ }
        }
    }

    [Fact]
    public async Task PrepareStagingFileAsync_ShouldPreallocateExpectedSize()
    {
        var manager = new PositionalFileManager();
        string tempFile = Path.Combine(_testDirectory, "test.velo.part");
        long expectedSize = 10 * 1024 * 1024; // 10 MB

        await manager.PrepareStagingFileAsync(tempFile, expectedSize);

        File.Exists(tempFile).Should().BeTrue();
        new FileInfo(tempFile).Length.Should().Be(expectedSize);
    }

    [Fact]
    public async Task WriteChunkAsync_MultiSegmentOffsets_ShouldWriteAtExactOffsets()
    {
        var manager = new PositionalFileManager();
        string tempFile = Path.Combine(_testDirectory, "segmented.velo.part");
        int totalSize = 200;

        await manager.PrepareStagingFileAsync(tempFile, totalSize);

        byte[] chunkA = Encoding.UTF8.GetBytes("CHUNK_A_START");
        byte[] chunkB = Encoding.UTF8.GetBytes("CHUNK_B_OFFSET_100");

        // Concurrently write at offset 0 and offset 100
        var taskA = manager.WriteChunkAsync(tempFile, 0, chunkA);
        var taskB = manager.WriteChunkAsync(tempFile, 100, chunkB);
        await Task.WhenAll(taskA, taskB);

        byte[] allBytes = await File.ReadAllBytesAsync(tempFile);
        allBytes.Length.Should().Be(totalSize);

        Encoding.UTF8.GetString(allBytes.AsSpan(0, chunkA.Length)).Should().Be("CHUNK_A_START");
        Encoding.UTF8.GetString(allBytes.AsSpan(100, chunkB.Length)).Should().Be("CHUNK_B_OFFSET_100");
    }

    [Fact]
    public async Task FinalizeFileAsync_ExistingDestination_ShouldResolveCollisionWithSuffix()
    {
        var manager = new PositionalFileManager();
        string tempFile = Path.Combine(_testDirectory, "download.velo.part");
        string finalFile = Path.Combine(_testDirectory, "document.pdf");

        await File.WriteAllTextAsync(tempFile, "Hello World Content");
        await File.WriteAllTextAsync(finalFile, "Existing Old Document");

        string resolvedPath = await manager.FinalizeFileAsync(tempFile, finalFile, overwrite: false);

        resolvedPath.Should().EndWith("document (1).pdf");
        File.Exists(resolvedPath).Should().BeTrue();
        File.Exists(finalFile).Should().BeTrue("Original file must not be overwritten");
        (await File.ReadAllTextAsync(resolvedPath)).Should().Be("Hello World Content");
    }

    [Fact]
    public async Task ComputeFileSha256Async_ShouldReturnCorrectHexHash()
    {
        var manager = new PositionalFileManager();
        string tempFile = Path.Combine(_testDirectory, "hash_test.txt");
        await File.WriteAllTextAsync(tempFile, "Velo Acceleration");

        string hash = await manager.ComputeFileSha256Async(tempFile);

        hash.Should().NotBeNullOrWhiteSpace();
        hash.Length.Should().Be(64); // SHA-256 is 64 hex characters
    }
}
