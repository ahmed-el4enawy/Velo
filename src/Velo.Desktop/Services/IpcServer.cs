using System.IO.Pipes;

namespace Velo.Desktop.Services;

/// <summary>
/// Background Named Pipe IPC listener allowing browser extension native host
/// to pass download URLs and control commands to the running Velo desktop instance.
/// Uses bidirectional pipe communication so the caller receives an immediate confirmation/rejection.
/// </summary>
public static class IpcServer
{
    public const string DefaultPipeName = "Velo_Ipc_Pipe_SingleInstance";
    public static string PipeName { get; set; } = DefaultPipeName;
    private static CancellationTokenSource? _cts;

    private static NamedPipeServerStream? _currentServer;

    public static void StartListening(Func<string, string> onMessageReceived, string? customPipeName = null)
    {
        Stop();
        PipeName = customPipeName ?? DefaultPipeName;
        _cts = new CancellationTokenSource();
        var token = _cts.Token;
        string activePipe = PipeName;

        _ = Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    using var server = new NamedPipeServerStream(
                        activePipe,
                        PipeDirection.InOut,
                        NamedPipeServerStream.MaxAllowedServerInstances,
                        PipeTransmissionMode.Byte,
                        PipeOptions.Asynchronous);

                    _currentServer = server;

                    await server.WaitForConnectionAsync(token);
                    using var reader = new StreamReader(server, System.Text.Encoding.UTF8, detectEncodingFromByteOrderMarks: false, bufferSize: 1024, leaveOpen: true);
                    using var writer = new StreamWriter(server, System.Text.Encoding.UTF8, bufferSize: 1024, leaveOpen: true) { AutoFlush = true };

                    string? line = await reader.ReadLineAsync(token);

                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        string response = onMessageReceived(line.Trim());
                        await writer.WriteLineAsync(response.AsMemory(), token);
                        await writer.FlushAsync(token);
                    }
                    else
                    {
                        await writer.WriteLineAsync("ACK:EMPTY".AsMemory(), token);
                        await writer.FlushAsync(token);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch
                {
                    if (token.IsCancellationRequested) break;
                    await Task.Delay(50, token);
                }
                finally
                {
                    _currentServer = null;
                }
            }
        }, token);
    }

    public static void Stop()
    {
        _cts?.Cancel();
        try
        {
            _currentServer?.Dispose();
        }
        catch
        {
            // Ignore disposal errors on shutdown
        }
        _currentServer = null;
    }
}
