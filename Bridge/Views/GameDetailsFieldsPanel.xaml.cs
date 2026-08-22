using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Bridge.Core.Entities;
using Bridge.ViewModels;

namespace Bridge.Views;

public partial class GameDetailsFieldsPanel : UserControl
{
    private const double InstallDirectoryLabelGap = 10;
    private const double InstallDirectoryMeasureSlack = 4;

    public GameDetailsFieldsPanel()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Dispatcher.BeginInvoke(UpdateInstallDirectoryLayout);
        Loaded += (_, _) => UpdateInstallDirectoryLayout();
    }

    private void InstallDirectoryHost_SizeChanged(object sender, SizeChangedEventArgs e) =>
        UpdateInstallDirectoryLayout();

    private void UpdateInstallDirectoryLayout()
    {
        if (DataContext is not Game { InstallDirectory: { Length: > 0 } path })
            return;

        if (InstallDirectoryHost.ActualWidth <= 0)
            return;

        InstallDirectoryInlineLabel.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var labelWidth = InstallDirectoryInlineLabel.DesiredSize.Width + InstallDirectoryLabelGap;
        var available = InstallDirectoryHost.ActualWidth - labelWidth;
        if (available <= 0)
            return;

        var measure = new TextBlock
        {
            Text = path,
            FontSize = 14,
            FontWeight = FontWeights.SemiBold,
            FontFamily = InstallDirectoryInlineLabel.FontFamily,
            TextWrapping = TextWrapping.NoWrap,
        };
        measure.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

        var fitsInline = measure.DesiredSize.Width <= available + InstallDirectoryMeasureSlack;
        InstallDirectoryInline.Visibility = fitsInline ? Visibility.Visible : Visibility.Collapsed;
        InstallDirectoryStacked.Visibility = fitsInline ? Visibility.Collapsed : Visibility.Visible;
    }

    private void CompanyFilterChip_RightClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Button { Content: string name })
            return;

        if (Window.GetWindow(this)?.DataContext is MainViewModel vm)
            vm.SearchGoogleCommand.Execute(name);

        e.Handled = true;
    }
}
