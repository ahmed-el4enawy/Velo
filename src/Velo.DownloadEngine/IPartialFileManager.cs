namespace Velo.DownloadEngine;

public interface IPartialFileManager
{
    Task PrepareStagingFileAsync(string tempPath, long? expectedBytes, CancellationToken cancellationToken = default);
    Task WriteChunkAsync(string tempPath, long offset, ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default);
    Task<string> FinalizeFileAsync(string tempPath, string targetPath, bool overwrite = false, CancellationToken cancellationToken = default);
    Task<string> ComputeFileSha256Async(string filePath, CancellationToken cancellationToken = default);
}
