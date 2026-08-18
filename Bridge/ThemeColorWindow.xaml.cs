using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Bridge.Services;
using Wpf.Ui.Controls;

namespace Bridge;

public partial class ThemeColorWindow : FluentWindow
{
    public ThemeColorWindow()
    {
        InitializeComponent();
        BuildSwatches();
    }

    private void BuildSwatches()
    {
        foreach (var color in ThemeManager.Palette)
        {
            var swatch = new Border
            {
                Width = 34,
                Height = 34,
                CornerRadius = new CornerRadius(4),
                Background = new SolidColorBrush(color),
                Margin = new Thickness(3),
                Cursor = Cursors.Hand,
                Tag = color,
                ToolTip = ThemeManager.ToHex(color)
            };

            swatch.MouseLeftButtonUp += (_, _) =>
            {
                Services.ThemeManager.Apply(color);
                DialogResult = true;
            };

            SwatchPanel.Children.Add(swatch);
        }
    }
}
