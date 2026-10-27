using FluentAssertions;
using Velo.Desktop.ViewModels;
using Velo.Domain;
using Xunit;

namespace Velo.EndToEndTests;

public class SettingsWindowViewModelTests
{
    private readonly FakeSettingsRepository _settingsRepo;

    public SettingsWindowViewModelTests()
    {
        _settingsRepo = new FakeSettingsRepository
        {
            CurrentSettings = new AppSettings
            {
                MaxConcurrentDownloads = 4,
                SegmentsPerDownload = 6,
                MaxConnectionsPerHost = 10,
                SpeedLimitBytesPerSecond = 1024 * 1024, // 1 MB/s
                ClipboardMonitoringEnabled = true,
                DefaultDownloadDirectory = @"C:\TestDownloads"
            }
        };
    }

    [Fact]
    public async Task LoadSettingsAsync_ShouldPopulateViewModelProperties()
    {
        var vm = new SettingsWindowViewModel(_settingsRepo);

        await vm.LoadSettingsAsync();

        vm.MaxConcurrentDownloads.Should().Be(4);
        vm.SegmentsPerDownload.Should().Be(6);
        vm.MaxConnectionsPerHost.Should().Be(10);
        vm.IsSpeedLimitEnabled.Should().BeTrue();
        vm.SpeedLimitKb.Should().Be(1024);
        vm.ClipboardMonitoringEnabled.Should().BeTrue();
        vm.DefaultDownloadDirectory.Should().Be(@"C:\TestDownloads");
    }

    [Fact]
    public async Task SaveAsync_ShouldPersistSettingsAndTriggerClose()
    {
        var vm = new SettingsWindowViewModel(_settingsRepo);

        bool closeRequested = false;
        vm.RequestClose += () => closeRequested = true;

        vm.MaxConcurrentDownloads = 5;
        vm.SegmentsPerDownload = 8;
        vm.IsSpeedLimitEnabled = false;
        vm.ClipboardMonitoringEnabled = false;

        await vm.SaveCommand.ExecuteAsync(null);

        closeRequested.Should().BeTrue();
        _settingsRepo.SaveCalled.Should().BeTrue();
        _settingsRepo.CurrentSettings.MaxConcurrentDownloads.Should().Be(5);
        _settingsRepo.CurrentSettings.SegmentsPerDownload.Should().Be(8);
        _settingsRepo.CurrentSettings.SpeedLimitBytesPerSecond.Should().BeNull();
        _settingsRepo.CurrentSettings.ClipboardMonitoringEnabled.Should().BeFalse();
    }
}
