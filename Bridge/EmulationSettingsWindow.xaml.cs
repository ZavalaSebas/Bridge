using Bridge.ViewModels;
using Wpf.Ui.Controls;

namespace Bridge;

public partial class EmulationSettingsWindow : FluentWindow
{
    public EmulationSettingsWindow(EmulationSettingsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        Loaded += async (_, _) => await viewModel.LoadAsync();
    }
}
