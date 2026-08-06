using System.Windows;
using Bridge.ViewModels;

namespace Bridge;

public partial class IgdbSettingsWindow : Window
{
    public IgdbSettingsWindow(IgdbSettingsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.Saved += () => DialogResult = true;
    }
}
