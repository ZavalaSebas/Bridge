using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using Bridge.ViewModels;

namespace Bridge.Converters;

public class StatusMessageKindToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is StatusMessageKind.Error
            ? Application.Current.FindResource("SystemFillColorCriticalBrush") as Brush
                ?? new SolidColorBrush(Color.FromRgb(0xFF, 0x6B, 0x6B))
            : value is StatusMessageKind.Warning
                ? Application.Current.FindResource("SystemFillColorCautionBrush") as Brush
                    ?? new SolidColorBrush(Color.FromRgb(0xFF, 0xC1, 0x07))
                : Application.Current.FindResource("TextFillColorSecondaryBrush") as Brush
                    ?? Brushes.Gray;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
