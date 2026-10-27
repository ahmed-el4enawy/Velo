using Avalonia.Controls;
using Velo.Desktop.ViewModels;

namespace Velo.Desktop.Views;

public partial class DownloadProgressWindow : Window
{
    public DownloadProgressWindow()
    {
        InitializeComponent();
    }

    public DownloadProgressWindow(DownloadProgressWindowViewModel viewModel) : this()
    {
        DataContext = viewModel;
        viewModel.RequestClose += () => Close();
    }
}
