using System.Windows;

namespace Bridge.Controls;

/// <summary>
/// Holds a DataContext reference in a ResourceDictionary so bindings inside a
/// nested DataContext (e.g. hero panel bound to SelectedGame) can still reach
/// the parent ViewModel.
/// </summary>
public class BindingProxy : Freezable
{
    public object? Data
    {
        get => GetValue(DataProperty);
        set => SetValue(DataProperty, value);
    }

    public static readonly DependencyProperty DataProperty =
        DependencyProperty.Register(nameof(Data), typeof(object), typeof(BindingProxy));

    protected override Freezable CreateInstanceCore() => new BindingProxy();
}
