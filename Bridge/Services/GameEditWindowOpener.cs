using System.Windows;
using Bridge.Core.Entities;
using Bridge.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace Bridge.Services;

public sealed class GameEditWindowOpener(IServiceProvider services)
{
    public bool Show(Game game, bool selectMediaTab = false, Window? owner = null)
    {
        var editViewModel = services.GetRequiredService<GameEditViewModelFactory>().Create(game);
        var window = new GameEditWindow(editViewModel, game.BackgroundImage)
        {
            Owner = owner ?? Application.Current.MainWindow as Window
        };

        if (selectMediaTab)
            window.SelectMediaTab();

        return window.ShowDialog() == true;
    }
}
