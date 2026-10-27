using System.IO.Pipes;

namespace Velo.Desktop.Services;

/// <summary>
/// Background Named Pipe IPC listener allowing browser extension native host
/// to pass download URLs to the running Velo desktop instance.
/// </summary>
public static class IpcServer
{
    public const string PipeName = "Velo_Ipc_Pipe_SingleInstance";
    private static CancellationTokenSource? _cts;

    public static void StartListening(Action<string> onMessageReceived)
    {
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        _ = Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    using var server = new NamedPipeServerStream(
                        PipeName,
                        PipeDirection.In,
                        NamedPipeServerStream.MaxAllowedServerInstances,
                        PipeTransmissionMode.Byte,
                        PipeOptions.Asynchronous);

                    await server.WaitForConnectionAsync(token);
                    using var reader = new StreamReader(server);
                    string? line = await reader.ReadLineAsync(token);

                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        onMessageReceived(line.Trim());
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch
                {
                    await Task.Delay(250, token);
                }
            }
        }, token);
    }

    public static void Stop()
    {
        _cts?.Cancel();
    }
}
