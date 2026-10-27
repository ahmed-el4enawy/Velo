using System.Security.Cryptography;
using Microsoft.Win32.SafeHandles;

namespace Velo.DownloadEngine;

/// <summary>
/// Thread-safe positional disk writer and staging manager using .NET RandomAccess I/O (PRD v2.0 Section 5.2 & FR-08).
/// Writes parallel segments into fixed byte offsets of a single `.velo.part` staging file without corrupting other chunks.
/// </summary>
public sealed class PositionalFileManager : IPartialFileManager
{
    public Task PrepareStagingFileAsync(string tempPath, long? expectedBytes, CancellationToken cancellationToken = default)
    {
        string? directory = Path.GetDirectoryName(tempPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // Verify available disk space
        if (expectedBytes is > 0)
        {
            var drive = new DriveInfo(Path.GetPathRoot(Path.GetFullPath(tempPath)) ?? "C:\\");
            if (drive.AvailableFreeSpace < expectedBytes.Value)
            {
                throw new IOException($"Insufficient disk space on drive {drive.Name}. Required: {expectedBytes.Value:N0} bytes, Available: {drive.AvailableFreeSpace:N0} bytes.");
            }
        }

        // Pre-allocate or create the staging file
        using var stream = new FileStream(
            tempPath,
            FileMode.OpenOrCreate,
            FileAccess.ReadWrite,
            FileShare.ReadWrite,
            bufferSize: 4096,
            FileOptions.Asynchronous);

        if (expectedBytes is > 0 && stream.Length < expectedBytes.Value)
        {
            stream.SetLength(expectedBytes.Value);
        }

        return Task.CompletedTask;
    }

    public async Task WriteChunkAsync(string tempPath, long offset, ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        using SafeFileHandle handle = File.OpenHandle(
            tempPath,
            FileMode.Open,
            FileAccess.Write,
            FileShare.ReadWrite,
            FileOptions.Asynchronous);

        await RandomAccess.WriteAsync(handle, buffer, offset, cancellationToken);
    }

    public Task<string> FinalizeFileAsync(string tempPath, string targetPath, bool overwrite = false, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(tempPath))
        {
            throw new FileNotFoundException("Temporary staging file was not found.", tempPath);
        }

        string? targetDir = Path.GetDirectoryName(targetPath);
        if (!string.IsNullOrEmpty(targetDir) && !Directory.Exists(targetDir))
        {
            Directory.CreateDirectory(targetDir);
        }

        string finalDestination = targetPath;

        if (!overwrite && File.Exists(finalDestination))
        {
            string dir = Path.GetDirectoryName(targetPath) ?? string.Empty;
            string fileNameWithoutExt = Path.GetFileNameWithoutExtension(targetPath);
            string ext = Path.GetExtension(targetPath);

            int counter = 1;
            while (File.Exists(finalDestination))
            {
                finalDestination = Path.Combine(dir, $"{fileNameWithoutExt} ({counter}){ext}");
                counter++;
            }
        }

        File.Move(tempPath, finalDestination, overwrite);
        return Task.FromResult(finalDestination);
    }

    public async Task<string> ComputeFileSha256Async(string filePath, CancellationToken cancellationToken = default)
    {
        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
        var hashBytes = await SHA256.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexStringLower(hashBytes);
    }
}
