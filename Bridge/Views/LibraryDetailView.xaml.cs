using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Bridge.Core.Entities;
using Bridge.Services;
using Bridge.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace Bridge.Views;

public partial class LibraryDetailView : UserControl
{
    private static readonly TimeSpan FavoriteStarMotion = TimeSpan.FromMilliseconds(180);

    private readonly DispatcherTimer _favoriteHideTimer;
    private bool _suppressTableResize;

    public LibraryDetailView()
    {
        InitializeComponent();

        _favoriteHideTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _favoriteHideTimer.Tick += (_, _) =>
        {
            _favoriteHideTimer.Stop();
            if (CoverFavoriteButton.IsChecked != true && CoverHost.IsMouseOver is false)
                AnimateFavoriteStar(inView: false);
        };
    }

    private MainViewModel? ViewModel => Window.GetWindow(this)?.DataContext as MainViewModel;

    private void MenuButton_Click(object sender, RoutedEventArgs e)
    {
        if (Window.GetWindow(this) is MainWindow mainWindow)
            mainWindow.HandleMenuButtonClick(sender, e);
    }

    private void EditGame_Click(object sender, RoutedEventArgs e)
    {
        if (Window.GetWindow(this) is MainWindow mainWindow)
            mainWindow.HandleEditGameClick(sender, e);
    }

    private void CloseCompactInfo_Click(object sender, RoutedEventArgs e)
    {
        CompactInfoPanel.Visibility = Visibility.Collapsed;
    }

    private void TableList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ListView { SelectedItem: GameDetailRow row }
            && ViewModel is { } vm)
        {
            vm.SelectedGame = row.Game;
        }
    }

    private void TableList_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (sender is not ListView listView
            || listView.View is not GridView gridView
            || gridView.Columns.Count < 2)
            return;

        if (_suppressTableResize)
            return;

        _suppressTableResize = true;

        double totalFixed = 0;
        for (int i = 1; i < gridView.Columns.Count; i++)
            totalFixed += gridView.Columns[i].Width;

        double available = listView.ActualWidth
                           - SystemParameters.VerticalScrollBarWidth
                           - totalFixed;

        if (available < 100)
        {
            _suppressTableResize = false;
            return;
        }

        var nameColumn = gridView.Columns[0];
        if (Math.Abs(nameColumn.Width - available) > 0.5)
        {
            double capture = available;
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded,
                new Action(() =>
                {
                    nameColumn.Width = capture;
                    ScrollPositionSettingsStore.SaveTableNameWidth(capture);
                    _suppressTableResize = false;
                }));
        }
        else
        {
            _suppressTableResize = false;
        }
    }

    private void List_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is ListBox listBox
            && listBox.SelectedItem is Game game
            && ViewModel is { } vm)
        {
            vm.PlayGameCommand.Execute(game);
        }
    }

    private void GameInfo_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: Game game }
            && ViewModel is { } viewModel)
        {
            viewModel.SelectedGame = game;
            CompactInfoPanel.Visibility = Visibility.Visible;

            Dispatcher.BeginInvoke(DispatcherPriority.Loaded,
                new Action(() => CoversList.ScrollIntoView(game)));
        }
    }

    private void CoverFavorite_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
    {
        _favoriteHideTimer.Stop();
        AnimateFavoriteStar(inView: true);
    }

    private void CoverFavorite_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (CoverFavoriteButton.IsChecked != true)
            _favoriteHideTimer.Start();
    }

    private void CoverFavorite_Checked(object sender, RoutedEventArgs e)
    {
        _favoriteHideTimer.Stop();
        AnimateFavoriteStar(inView: true);
        if (CoverFavoriteButton.IsMouseOver)
        {
            PlayFavoritePop();
            PersistFavorite();
        }
    }

    private void CoverFavorite_Unchecked(object sender, RoutedEventArgs e)
    {
        if (CoverHost.IsMouseOver is false)
            AnimateFavoriteStar(inView: false);

        if (CoverFavoriteButton.IsMouseOver)
            PersistFavorite();
    }

    private void CompactFavorite_Checked(object sender, RoutedEventArgs e)
    {
        if (!CompactFavoriteButton.IsMouseOver)
            return;

        PlayCompactFavoritePop();
        PersistFavorite();
    }

    private void CompactFavorite_Unchecked(object sender, RoutedEventArgs e)
    {
        if (CompactFavoriteButton.IsMouseOver)
            PersistFavorite();
    }

    private void PersistFavorite()
    {
        ViewModel?.PersistFavorite();
    }

    private void PlayCompactFavoritePop()
    {
        if (CompactFavoriteScale is null)
            return;

        var pop = BuildPopAnimation();
        CompactFavoriteScale.BeginAnimation(ScaleTransform.ScaleXProperty, pop);
        CompactFavoriteScale.BeginAnimation(ScaleTransform.ScaleYProperty, pop);
    }

    private void AnimateFavoriteStar(bool inView)
    {
        if (CoverFavoriteButton is null)
            return;

        CoverFavoriteTranslate.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(inView ? 0 : 26, FavoriteStarMotion) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } });
        CoverFavoriteButton.BeginAnimation(OpacityProperty, new DoubleAnimation(inView ? 1 : 0, FavoriteStarMotion));
        CoverFavoriteButton.IsHitTestVisible = inView;
    }

    private void PlayFavoritePop()
    {
        var pop = BuildPopAnimation();
        CoverFavoriteScale.BeginAnimation(ScaleTransform.ScaleXProperty, pop);
        CoverFavoriteScale.BeginAnimation(ScaleTransform.ScaleYProperty, pop);
    }

    private static DoubleAnimationUsingKeyFrames BuildPopAnimation()
    {
        var animation = new DoubleAnimationUsingKeyFrames();
        animation.KeyFrames.Add(new EasingDoubleKeyFrame(0.5, KeyTime.FromTimeSpan(TimeSpan.Zero)));
        animation.KeyFrames.Add(new EasingDoubleKeyFrame(1.35, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(140)), new QuadraticEase { EasingMode = EasingMode.EaseOut }));
        animation.KeyFrames.Add(new EasingDoubleKeyFrame(0.9, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(260)), new QuadraticEase { EasingMode = EasingMode.EaseIn }));
        animation.KeyFrames.Add(new EasingDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(380)), new QuadraticEase { EasingMode = EasingMode.EaseOut }));
        return animation;
    }
}
