using System.Windows;
using Bridge.Core.Entities;
using Bridge.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace Bridge.Services;

public sealed class CheatsWindowOpener(IServiceProvider services)
{
    public void Show(Game game, Window? owner = null)
    {
        var viewModel = services.GetRequiredService<CheatsViewModel>();
        viewModel.SetGame(game);
        var window = new CheatsWindow(viewModel)
        {
            Owner = owner ?? Application.Current.MainWindow as Window
        };
        window.ShowDialog();
    }
}
