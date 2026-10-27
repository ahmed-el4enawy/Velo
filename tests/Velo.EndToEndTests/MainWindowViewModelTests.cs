using FluentAssertions;
using Velo.Desktop.ViewModels;
using Xunit;

namespace Velo.EndToEndTests;

public class MainWindowViewModelTests
{
    [Fact]
    public void MainWindowViewModel_InitialState_ShouldBeValid()
    {
        var vm = new MainWindowViewModel();

        vm.AppTitle.Should().Contain("Velo");
        vm.Downloads.Should().BeEmpty();
        vm.ActiveCategory.Should().Be("All Downloads");
        vm.TotalDownloadSpeedBytesPerSec.Should().Be(0);
    }
}
