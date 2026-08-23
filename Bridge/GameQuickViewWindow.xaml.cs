using System.Windows;
using System.Windows.Input;
using Bridge.Core.Entities;
using Bridge.ViewModels;

namespace Bridge;

public partial class GameQuickViewWindow : Window
{
    private readonly Game _game;
    private readonly MainViewModel _vm;

    public GameQuickViewWindow(Game game, MainViewModel vm)
    {
        InitializeComponent();
        _game = game;
        _vm = vm;
        DataContext = game;
        // Enable drag for borderless window
        MouseLeftButtonDown += (_, e) =>
        {
            if (e.ButtonState == MouseButtonState.Pressed)
                DragMove();
        };
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void Play_Click(object sender, RoutedEventArgs e)
    {
        _vm.SelectedGame = _game;
        _vm.NavigationSection = Bridge.Core.Enums.NavigationSection.Library;
        if (_game.IsRunning)
            _vm.StopGameCommand.Execute(_game);
        else
            _vm.PlayGameCommand.Execute(_game);
        Close();
    }

    private void OpenDetails_Click(object sender, RoutedEventArgs e)
    {
        _vm.SelectedGame = _game;
        _vm.NavigationSection = Bridge.Core.Enums.NavigationSection.Library;
        Close();
    }
}
