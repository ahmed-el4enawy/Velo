using System.Text.Json;
using Velo.Domain;

namespace Velo.NativeMessaging;

/// <summary>
/// Native Messaging Host executable for Chrome, Edge, and Firefox companion extensions (PRD v2.0 Section 9).
/// Communicates using standard 32-bit length-prefixed JSON protocol on stdin/stdout.
/// </summary>
internal static class Program
{
    public static async Task Main(string[] args)
    {
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

            // Acknowledge receipt
            var response = JsonSerializer.SerializeToUtf8Bytes(new { status = "acknowledged", version = "1.0.0" });
            var responseLen = BitConverter.GetBytes(response.Length);
            await stdout.WriteAsync(responseLen, 0, 4);
            await stdout.WriteAsync(response, 0, response.Length);
            await stdout.FlushAsync();
        }
    }
}
