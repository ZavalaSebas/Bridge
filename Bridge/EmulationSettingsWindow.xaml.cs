using System.Windows;
using Bridge.ViewModels;

namespace Bridge;

public partial class EmulationSettingsWindow : Window
{
    public EmulationSettingsWindow(EmulationSettingsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        Loaded += async (_, _) => await viewModel.LoadAsync();
    }
}
