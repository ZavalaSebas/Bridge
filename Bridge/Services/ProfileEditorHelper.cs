using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WpfButton = System.Windows.Controls.Button;

namespace Bridge.Services;

/// <summary>Shared avatar picker UI used by the setup wizard and settings.</summary>
public static class ProfileEditorHelper
{
    public sealed class AvatarEditorState
    {
        public string SelectedAvatarId { get; set; } = UserProfileAvatarHelper.DefaultAvatarIds[0];
        public bool UseCustomAvatar { get; set; }
        public string CustomAvatarPath { get; set; } = string.Empty;
    }

    public static void PopulateDefaultAvatars(
        Panel host,
        AvatarEditorState state,
        Action refreshPreview,
        ResourceDictionary resources)
    {
        host.Children.Clear();
        foreach (var avatarId in UserProfileAvatarHelper.DefaultAvatarIds)
        {
            var color = UserProfileAvatarHelper.GetDefaultColor(avatarId);
            var button = new WpfButton
            {
                Width = 44,
                Height = 44,
                Margin = new Thickness(0, 0, 8, 0),
                Tag = avatarId,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(2),
                BorderBrush = Brushes.Transparent,
                Padding = new Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand
            };

            button.Content = new Border
            {
                Width = 36,
                Height = 36,
                CornerRadius = new CornerRadius(18),
                Background = new SolidColorBrush(color),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            button.Click += (_, _) =>
            {
                state.UseCustomAvatar = false;
                state.CustomAvatarPath = string.Empty;
                SelectDefaultAvatar(host, avatarId, state, resources);
                refreshPreview();
            };

            host.Children.Add(button);
        }

        SelectDefaultAvatar(host, state.SelectedAvatarId, state, resources);
    }

    public static void SelectDefaultAvatar(
        Panel host,
        string avatarId,
        AvatarEditorState state,
        ResourceDictionary resources)
    {
        state.SelectedAvatarId = avatarId;
        var accent = resources["SystemAccentColorPrimaryBrush"] as Brush ?? Brushes.DodgerBlue;
        foreach (var child in host.Children)
        {
            if (child is WpfButton button)
            {
                button.BorderBrush = string.Equals(button.Tag as string, avatarId, StringComparison.OrdinalIgnoreCase)
                    ? accent
                    : Brushes.Transparent;
            }
        }
    }

    public static UserProfile ToProfile(AvatarEditorState state, string displayName) =>
        new()
        {
            DisplayName = displayName.Trim(),
            DefaultAvatarId = state.SelectedAvatarId,
            UseCustomAvatar = state.UseCustomAvatar,
            CustomAvatarPath = state.CustomAvatarPath
        };

    public static AvatarEditorState FromProfile(UserProfile profile) =>
        new()
        {
            SelectedAvatarId = profile.DefaultAvatarId,
            UseCustomAvatar = profile.UseCustomAvatar,
            CustomAvatarPath = profile.CustomAvatarPath
        };

    public static void RefreshPreview(System.Windows.Controls.Image preview, UserProfile profile) =>
        preview.Source = UserProfileAvatarHelper.GetAvatarImage(profile, 144);
}
