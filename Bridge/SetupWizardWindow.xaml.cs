using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Bridge.Import.Epic;
using Bridge.Import.Steam;
using Bridge.Resources;
using Bridge.Services;
using Microsoft.Win32;
using WpfButton = System.Windows.Controls.Button;

namespace Bridge;

public partial class SetupWizardWindow
{
    private const int StepCount = 3;

    private int _stepIndex;
    private string _selectedAvatarId = UserProfileAvatarHelper.DefaultAvatarIds[0];
    private bool _useCustomAvatar;
    private string _customAvatarPath = string.Empty;

    public SetupWizardResult? Result { get; private set; }

    public SetupWizardWindow()
    {
        InitializeComponent();
        App.ApplyWindowIcon(this);
        BuildDefaultAvatarChoices();
        ConfigureStoreDetection();
        ShowStep(0);
    }

    private void BuildDefaultAvatarChoices()
    {
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

            var circle = new Border
            {
                Width = 36,
                Height = 36,
                CornerRadius = new CornerRadius(18),
                Background = new SolidColorBrush(color),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            button.Content = circle;
            button.Click += DefaultAvatar_Click;
            DefaultAvatarGrid.Children.Add(button);
        }

        SelectDefaultAvatar(_selectedAvatarId);
        RefreshAvatarPreview();
    }

    private void ConfigureStoreDetection()
    {
        ConfigureStoreCard(
            detected: !string.IsNullOrEmpty(SteamPaths.GetInstallationPath()),
            card: SteamDetectedCard,
            statusText: SteamStatusText,
            statusHelp: SteamStatusHelp,
            detectedTitle: Strings.SetupSteamDetected,
            detectedHelp: Strings.SetupSteamDetectedHelp,
            notDetectedTitle: Strings.SetupSteamNotDetected,
            notDetectedHelp: Strings.SetupSteamNotDetectedHelp);

        ConfigureStoreCard(
            detected: EpicPaths.IsInstalled,
            card: EpicDetectedCard,
            statusText: EpicStatusText,
            statusHelp: EpicStatusHelp,
            detectedTitle: Strings.SetupEpicDetected,
            detectedHelp: Strings.SetupEpicDetectedHelp,
            notDetectedTitle: Strings.SetupEpicNotDetected,
            notDetectedHelp: Strings.SetupEpicNotDetectedHelp);
    }

    private static void ConfigureStoreCard(
        bool detected,
        Border card,
        System.Windows.Controls.TextBlock statusText,
        System.Windows.Controls.TextBlock statusHelp,
        string detectedTitle,
        string detectedHelp,
        string notDetectedTitle,
        string notDetectedHelp)
    {
        statusText.Text = detected ? detectedTitle : notDetectedTitle;
        statusHelp.Text = detected ? detectedHelp : notDetectedHelp;
        card.Opacity = detected ? 1.0 : 0.72;
    }

    private void ShowStep(int stepIndex)
    {
        _stepIndex = stepIndex;
        ProfileStep.Visibility = stepIndex == 0 ? Visibility.Visible : Visibility.Collapsed;
        LibrariesStep.Visibility = stepIndex == 1 ? Visibility.Visible : Visibility.Collapsed;
        RomFolderStep.Visibility = stepIndex == 2 ? Visibility.Visible : Visibility.Collapsed;

        HeaderTitle.Text = Strings.SetupWizardTitle;
        BackButton.Visibility = stepIndex == 0 ? Visibility.Collapsed : Visibility.Visible;
        NextButton.Content = stepIndex == StepCount - 1 ? Strings.SetupGetStarted : Strings.SetupNext;
        StepIndicator.Text = Strings.Format(nameof(Strings.SetupStepFormat), stepIndex + 1, StepCount);

        switch (stepIndex)
        {
            case 0:
                StepTitle.Text = Strings.SetupProfileStepTitle;
                StepDescription.Text = Strings.SetupProfileStepDescription;
                DisplayNameBox.Focus();
                break;
            case 1:
                StepTitle.Text = Strings.SetupLibrariesStepTitle;
                StepDescription.Text = Strings.SetupLibrariesStepDescription;
                break;
            default:
                StepTitle.Text = Strings.SetupRomStepTitle;
                StepDescription.Text = Strings.SetupRomStepDescription;
                break;
        }
    }

    private void RefreshAvatarPreview()
    {
        var profile = BuildProfileDraft();
        AvatarPreview.Source = UserProfileAvatarHelper.GetAvatarImage(profile, 144);
    }

    private UserProfile BuildProfileDraft() =>
        new()
        {
            DisplayName = DisplayNameBox.Text.Trim(),
            DefaultAvatarId = _selectedAvatarId,
            UseCustomAvatar = _useCustomAvatar,
            CustomAvatarPath = _customAvatarPath
        };

    private void DefaultAvatar_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not WpfButton { Tag: string avatarId })
            return;

        _useCustomAvatar = false;
        _customAvatarPath = string.Empty;
        SelectDefaultAvatar(avatarId);
        RefreshAvatarPreview();
    }

    private void SelectDefaultAvatar(string avatarId)
    {
        _selectedAvatarId = avatarId;
        foreach (var child in DefaultAvatarGrid.Children)
        {
            if (child is WpfButton button)
            {
                button.BorderBrush = string.Equals(button.Tag as string, avatarId, StringComparison.OrdinalIgnoreCase)
                    ? (Brush)FindResource("SystemAccentColorPrimaryBrush")
                    : Brushes.Transparent;
            }
        }
    }

    private void ChoosePhoto_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = Strings.SetupChoosePhoto,
            Filter = Strings.SetupPhotoFilter
        };
        if (dialog.ShowDialog(this) != true)
            return;

        _customAvatarPath = UserProfileAvatarHelper.SaveCustomAvatar(dialog.FileName);
        _useCustomAvatar = true;
        foreach (var child in DefaultAvatarGrid.Children)
        {
            if (child is WpfButton button)
                button.BorderBrush = Brushes.Transparent;
        }

        RefreshAvatarPreview();
    }

    private void UseDefaultAvatar_Click(object sender, RoutedEventArgs e)
    {
        _useCustomAvatar = false;
        _customAvatarPath = string.Empty;
        SelectDefaultAvatar(_selectedAvatarId);
        RefreshAvatarPreview();
    }

    private void BrowseExternalGamesFolder_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = Strings.SetupExternalGamesFolderTitle };
        if (dialog.ShowDialog(this) == true)
            ExternalGamesFolderBox.Text = dialog.FolderName;
    }

    private void BrowseRomFolder_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = Strings.SetupRomFolderTitle };
        if (dialog.ShowDialog(this) == true)
            RomFolderBox.Text = dialog.FolderName;
    }

    private void Back_Click(object sender, RoutedEventArgs e)
    {
        if (_stepIndex > 0)
            ShowStep(_stepIndex - 1);
    }

    private void Next_Click(object sender, RoutedEventArgs e)
    {
        if (_stepIndex == 0 && string.IsNullOrWhiteSpace(DisplayNameBox.Text))
        {
            MessageDialogWindow.ShowWarning(Strings.SetupDisplayNameRequired, Strings.SetupWizardTitle, this);
            DisplayNameBox.Focus();
            return;
        }

        if (_stepIndex < StepCount - 1)
        {
            ShowStep(_stepIndex + 1);
            return;
        }

        var externalFolder = ExternalGamesFolderBox.Text.Trim();
        var romFolder = RomFolderBox.Text.Trim();
        if (!string.IsNullOrWhiteSpace(externalFolder) && !Directory.Exists(externalFolder))
        {
            MessageDialogWindow.ShowWarning(Strings.SetupInvalidExternalGamesFolder, Strings.SetupWizardTitle, this);
            ShowStep(1);
            return;
        }

        if (!string.IsNullOrWhiteSpace(romFolder) && !Directory.Exists(romFolder))
        {
            MessageDialogWindow.ShowWarning(Strings.SetupInvalidRomFolder, Strings.SetupWizardTitle, this);
            return;
        }

        Result = new SetupWizardResult
        {
            Profile = BuildProfileDraft(),
            ExternalGamesFolder = string.IsNullOrWhiteSpace(externalFolder) ? null : externalFolder,
            RomFolder = string.IsNullOrWhiteSpace(romFolder) ? null : romFolder
        };
        DialogResult = true;
    }
}
