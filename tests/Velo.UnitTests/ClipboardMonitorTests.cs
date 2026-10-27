using FluentAssertions;
using Velo.Application.Services;
using Velo.Domain;
using Xunit;

namespace Velo.UnitTests;

public class ClipboardMonitorTests
{
    [Fact]
    public void TryInspectText_ValidDownloadUrl_ShouldReturnTrueAndClassify()
    {
        var monitor = new ClipboardMonitorService();
        string testUrl = "https://speedtest.server.com/downloads/v1.0.0.zip";

        bool result = monitor.TryInspectText(testUrl, out string validUrl, out CategoryType category);

        result.Should().BeTrue();
        validUrl.Should().Be(testUrl);
        category.Should().Be(CategoryType.Compressed);
    }

    [Fact]
    public void TryInspectText_BareDomainOrNoExtension_ShouldReturnFalse()
    {
        var monitor = new ClipboardMonitorService();
        string testUrl = "https://www.google.com/";

        bool result = monitor.TryInspectText(testUrl, out _, out _);

        result.Should().BeFalse("Passive sniffer must ignore bare homepages without file targets");
    }

    [Fact]
    public void TryInspectText_DuplicateText_ShouldIgnoreSecondCall()
    {
        var monitor = new ClipboardMonitorService();
        string testUrl = "https://example.com/audio.mp3";

        monitor.TryInspectText(testUrl, out _, out _).Should().BeTrue();
        monitor.TryInspectText(testUrl, out _, out _).Should().BeFalse("Duplicate consecutive clipboard content must be ignored");
    }

    [Fact]
    public void TryInspectText_WhenEnabled_ShouldFireEvent()
    {
        var monitor = new ClipboardMonitorService { IsEnabled = true };
        string testUrl = "https://example.com/video.mp4";

        string? detectedUrl = null;
        CategoryType? detectedCat = null;
        monitor.DownloadableUrlDetected += (url, cat) =>
        {
            detectedUrl = url;
            detectedCat = cat;
        };

        monitor.TryInspectText(testUrl, out _, out _);

        detectedUrl.Should().Be(testUrl);
        detectedCat.Should().Be(CategoryType.Video);
    }
}
