using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Bridge;

public partial class MainWindow
{
    // The favorite star sits to the cover's left, tucked behind its edge
    // (translated toward the cover, which renders on top and clips it).
    // Hovering the cover area slides it out; once checked it stays visible.
    // Unchecking it while not hovering tucks it back away.
    private static readonly TimeSpan FavoriteStarMotion = TimeSpan.FromMilliseconds(180);

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
        // Only pop on a real click — a favorited game picked from the list
        // also fires Checked via binding, and popping then would be noise.
        if (CoverFavoriteButton.IsMouseOver)
        {
            PlayFavoritePop();
            PersistFavorite();
        }
    }

    private void CoverFavorite_Unchecked(object sender, RoutedEventArgs e)
    {
        // Only tuck it away when the mouse isn't over the cover — otherwise
        // unchecking while hovering would hide the very thing being hovered.
        if (CoverHost.IsMouseOver is false)
            AnimateFavoriteStar(inView: false);

        // Same guard as Checked: binding fires this on game selection too,
        // so only persist an actual user click.
        if (CoverFavoriteButton.IsMouseOver)
            PersistFavorite();
    }

    private void PersistFavorite()
    {
        if (DataContext is ViewModels.MainViewModel vm)
            vm.PersistFavorite();
    }

    // Same persistence pattern as the hero star, for the compact panel's
    // favorite star: binding fires Checked/Unchecked on game selection too,
    // so only persist an actual user click (IsMouseOver on the star). The
    // pop plays only on a real click for the same reason.
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

    // Same spring-back "pop" as the hero star (reuses BuildPopAnimation).
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
        // A tucked-away star must not catch the mouse (it would sit over the
        // seam and block the cover's hover); only the visible one is clickable.
        CoverFavoriteButton.IsHitTestVisible = inView;
    }

    // The star "pops" when favorited: it overshoots past its final size and
    // springs back, like a sticker being pressed on. Driven by keyframes so
    // the peak is part of the same timeline instead of two chained tweens.
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
