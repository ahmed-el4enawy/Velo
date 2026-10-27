using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Velo.Domain;

namespace Velo.Desktop.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _appTitle = "Velo — Next-Gen Download Accelerator";

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private string _activeCategory = "All Downloads";

    [ObservableProperty]
    private long _totalDownloadSpeedBytesPerSec;

    public ObservableCollection<DownloadJob> Downloads { get; } = [];

    [RelayCommand]
    private void OpenAddDownloadDialog()
    {
        // Handled by view or dialog service
    }

    [RelayCommand]
    private void PauseAll()
    {
        // Pause active jobs
    }

    [RelayCommand]
    private void ResumeAll()
    {
        // Resume queued jobs
    }
}
