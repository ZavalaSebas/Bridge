using Bridge.ViewModels;
using Wpf.Ui.Controls;

namespace Bridge;

public partial class EmulatorSetupWindow : FluentWindow
{
    public EmulatorSetupWindow(EmulatorSetupViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.Saved += () => DialogResult = true;
    }
}
