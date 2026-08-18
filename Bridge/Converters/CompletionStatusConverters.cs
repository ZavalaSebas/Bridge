using System.Globalization;
using System.Windows;
using System.Windows.Data;
using Bridge.Core.Contracts;
using Bridge.Core.Entities;
using Microsoft.Extensions.DependencyInjection;

namespace Bridge.Converters;

internal static class CompletionStatusConverterHelpers
{
    internal static string ResolveName(object? value)
    {
        return value switch
        {
            string name when !string.IsNullOrWhiteSpace(name) => name,
            Guid id when id != Guid.Empty => LookupName(id),
            _ => string.Empty
        };
    }

    private static string LookupName(Guid id)
    {
        if (App.Services is null)
            return string.Empty;

        return App.Services.GetRequiredService<IRepository<CompletionStatus>>().Get(id)?.Name ?? string.Empty;
    }
}

/// <summary>Resolves a completion status id (or name) to its display label.</summary>
public class CompletionStatusIdToTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => CompletionStatusConverterHelpers.ResolveName(value);

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Visible when a completion status id is set.</summary>
public class EmptyGuidToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is Guid guid)
            return guid == Guid.Empty ? Visibility.Collapsed : Visibility.Visible;

        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
