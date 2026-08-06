using System.Windows;
using Bridge.ViewModels;

namespace Bridge;

public partial class EmulatorSetupWindow : Window
{
    public EmulatorSetupWindow(EmulatorSetupViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.Saved += () => DialogResult = true;
    }
}
