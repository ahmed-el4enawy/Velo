using System.Diagnostics;
using System.IO.Pipes;
using System.Security.Cryptography;
using System.Text.Json;

namespace Velo.NativeMessaging;

/// <summary>
/// Native Messaging Host executable for Chrome, Edge, and Firefox companion extensions (PRD v2.0 Section 9).
/// Communicates using standard 32-bit length-prefixed JSON protocol on stdin/stdout.
/// Bridges extension downloads directly into Velo Desktop via Named Pipe IPC or process activation.
/// </summary>
internal static class Program
{
    private const string PipeName = "Velo_Ipc_Pipe_SingleInstance";

    public static async Task Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "--gen-key")
        {
            using var rsa = RSA.Create(2048);
            byte[] pubBytes = rsa.ExportSubjectPublicKeyInfo();
            string pubBase64 = Convert.ToBase64String(pubBytes);

            byte[] hash = SHA256.HashData(pubBytes);
            var idChars = new char[32];
            for (int i = 0; i < 16; i++)
            {
                byte b = hash[i];
                idChars[i * 2] = (char)('a' + (b >> 4));
                idChars[i * 2 + 1] = (char)('a' + (b & 0x0F));
            }

            string extId = new string(idChars);
            Console.WriteLine($"EXTENSION_ID={extId}");
            Console.WriteLine($"PUBLIC_KEY={pubBase64}");
            return;
        }

        // Standard Native Messaging protocol loop
        using var stdin = Console.OpenStandardInput();
        using var stdout = Console.OpenStandardOutput();
        var buffer = new byte[4];

        while (true)
        {
            int bytesRead = await stdin.ReadAsync(buffer, 0, 4);
            if (bytesRead < 4)
                break; // Stream closed

            int length = BitConverter.ToInt32(buffer, 0);
            if (length <= 0 || length > 1024 * 1024)
                break; // Sanity limit: 1 MB max message payload

            var payloadBytes = new byte[length];
            int readTotal = 0;
            while (readTotal < length)
            {
                int chunk = await stdin.ReadAsync(payloadBytes, readTotal, length - readTotal);
                if (chunk == 0) break;
                readTotal += chunk;
            }

            // Parse request
            string json = System.Text.Encoding.UTF8.GetString(payloadBytes);
            object responseObj = new { status = "acknowledged" };

            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                string action = root.TryGetProperty("action", out var act) ? act.GetString() ?? "" : "";
                string url = root.TryGetProperty("url", out var u) ? u.GetString() ?? "" : "";

                if (action == "enqueue_download" && !string.IsNullOrWhiteSpace(url))
                {
                    if (Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
                        (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
                    {
                        bool handedOff = await TrySendViaIpcAsync(url);
                        if (!handedOff)
                        {
                            LaunchVeloDesktop(url);
                        }
                        responseObj = new { status = "enqueued", url = url };
                    }
                    else
                    {
                        responseObj = new { status = "error", message = "Invalid HTTP/HTTPS scheme" };
                    }
                }
                else if (action == "open_app")
                {
                    bool handedOff = await TrySendViaIpcAsync("__ACTIVATE__");
                    if (!handedOff)
                    {
                        LaunchVeloDesktop(null);
                    }
                    responseObj = new { status = "opened" };
                }
            }
            catch (Exception ex)
            {
                responseObj = new { status = "error", message = ex.Message };
            }

            // Write response
            var response = JsonSerializer.SerializeToUtf8Bytes(responseObj);
            var responseLen = BitConverter.GetBytes(response.Length);
            await stdout.WriteAsync(responseLen, 0, 4);
            await stdout.WriteAsync(response, 0, response.Length);
            await stdout.FlushAsync();
        }
    }

    private static async Task<bool> TrySendViaIpcAsync(string message)
    {
        try
        {
            using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
            await client.ConnectAsync(600); // 600ms timeout
            using var writer = new StreamWriter(client) { AutoFlush = true };
            await writer.WriteLineAsync(message);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void LaunchVeloDesktop(string? url)
    {
        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        string veloExe = Path.Combine(baseDir, "Velo.exe");

        // Fallback candidate search
        if (!File.Exists(veloExe))
        {
            string[] candidates = new[]
            {
                @"F:\Velo\publish\win-x64\Velo.exe",
                @"F:\ShenawiDownloadManager\publish\win-x64\Velo.exe",
                @"F:\Velo\src\Velo.Desktop\bin\x64\Release\net10.0\Velo.exe",
                @"F:\ShenawiDownloadManager\src\Velo.Desktop\bin\x64\Release\net10.0\Velo.exe",
                @"F:\Velo\src\Velo.Desktop\bin\Release\net10.0\Velo.exe",
                @"F:\ShenawiDownloadManager\src\Velo.Desktop\bin\Release\net10.0\Velo.exe"
            };

            foreach (var candidate in candidates)
            {
                if (File.Exists(candidate))
                {
                    veloExe = candidate;
                    break;
                }
            }
        }

        if (File.Exists(veloExe))
        {
            string args = !string.IsNullOrWhiteSpace(url) ? $"--url \"{url}\"" : "";
            Process.Start(new ProcessStartInfo(veloExe, args)
            {
                UseShellExecute = true,
                WorkingDirectory = Path.GetDirectoryName(veloExe)
            });
        }
    }
}
