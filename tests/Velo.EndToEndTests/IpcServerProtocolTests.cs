using System.IO.Pipes;
using FluentAssertions;
using Velo.Desktop.Services;
using Xunit;

namespace Velo.EndToEndTests;

public class IpcServerProtocolTests
{
    [Fact]
    public async Task IpcServer_ValidUrl_ReceivesAcceptedAcknowledgment()
    {
        // Arrange
        string testPipe = $"Velo_Test_Pipe_{Guid.NewGuid():N}";
        string received = "";
        IpcServer.StartListening(msg =>
        {
            received = msg;
            if (Uri.TryCreate(msg, UriKind.Absolute, out var uri) &&
                (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
            {
                return "ACK:ACCEPTED";
            }
            return "ACK:REJECTED_INVALID_SCHEME";
        }, testPipe);

        await Task.Delay(100);

        // Act
        string testUrl = "https://speed.cloudflare.com/100mb.bin";
        string reply = "";
        using (var client = new NamedPipeClientStream(".", testPipe, PipeDirection.InOut))
        {
            await client.ConnectAsync(2000);
            using var writer = new StreamWriter(client, System.Text.Encoding.UTF8, 1024, leaveOpen: true) { AutoFlush = true };
            using var reader = new StreamReader(client, System.Text.Encoding.UTF8, false, 1024, leaveOpen: true);

            await writer.WriteLineAsync(testUrl);
            reply = await reader.ReadLineAsync() ?? "";
        }

        // Assert
        received.Should().Be(testUrl);
        reply.Should().Be("ACK:ACCEPTED");

        IpcServer.Stop();
    }

    [Fact]
    public async Task IpcServer_InvalidScheme_ReceivesRejectedAcknowledgment()
    {
        // Arrange
        string testPipe = $"Velo_Test_Pipe_{Guid.NewGuid():N}";
        IpcServer.StartListening(msg =>
        {
            if (Uri.TryCreate(msg, UriKind.Absolute, out var uri) &&
                (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
            {
                return "ACK:ACCEPTED";
            }
            return "ACK:REJECTED_INVALID_SCHEME";
        }, testPipe);

        await Task.Delay(100);

        // Act
        string reply = "";
        using (var client = new NamedPipeClientStream(".", testPipe, PipeDirection.InOut))
        {
            await client.ConnectAsync(2000);
            using var writer = new StreamWriter(client, System.Text.Encoding.UTF8, 1024, leaveOpen: true) { AutoFlush = true };
            using var reader = new StreamReader(client, System.Text.Encoding.UTF8, false, 1024, leaveOpen: true);

            await writer.WriteLineAsync("javascript:alert(1)");
            reply = await reader.ReadLineAsync() ?? "";
        }

        // Assert
        reply.Should().Be("ACK:REJECTED_INVALID_SCHEME");

        IpcServer.Stop();
    }
}
