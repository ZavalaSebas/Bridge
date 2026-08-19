using Bridge.ViewModels;
using Wpf.Ui.Controls;

namespace Bridge;

public partial class CheatsWindow : FluentWindow
{
    public CheatsWindow(CheatsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        Loaded += CheatsWindow_Loaded;
    }

    private async void CheatsWindow_Loaded(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is CheatsViewModel viewModel)
        {
            await viewModel.InitializeAsync();
        }
    }
}
