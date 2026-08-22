using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Bridge.Resources;
using Bridge.Services;
using Bridge.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using Wpf.Ui.Controls;

namespace Bridge.Views;

public partial class SettingsOverlayView : UserControl
{
    private bool _loadingLanguage;
    private bool _loadingStartWithWindows;
    private bool _loadingTrayIcon;
    private bool _loadingKeepSelection;
    private bool _loadingDetailPanelPosition;
    private bool _loadingDetailSectionPosition;
    private bool _loadingTranslucentSidebar;
    private bool _loadingTranslucentBackground;
    private ProfileEditorHelper.AvatarEditorState _profileState = new();

    private static readonly string[] DetailPanelPositionValues =
    [
        DetailPanelPositionSettingsStore.Left,
        DetailPanelPositionSettingsStore.Right
    ];

    private static readonly string[] DetailSectionPositionValues =
    [
        DetailSectionPositionSettingsStore.Left,
        DetailSectionPositionSettingsStore.Right
    ];

    public SettingsOverlayView()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            BetaChannelToggle.IsChecked = UpdateChannelSettingsStore.Load() == UpdateChannel.Beta;

            _loadingLanguage = true;
            LanguageCombo.SelectedIndex =
                LanguageSettingsStore.Load() == AppLanguage.Spanish ? 1 : 0;
            _loadingLanguage = false;

            var canRegisterStartup = WindowsStartupRegistration.CanRegister;
            StartWithWindowsToggle.IsEnabled = canRegisterStartup;
            if (!canRegisterStartup)
                StartWithWindowsDescription.Text = Strings.SettingsStartWithWindowsUnavailable;

            _loadingStartWithWindows = true;
            StartWithWindowsToggle.IsChecked = StartupSettingsStore.Load();
            _loadingStartWithWindows = false;

            _loadingTrayIcon = true;
            TrayIconToggle.IsChecked = TrayIconSettingsStore.Load();
            _loadingTrayIcon = false;

            _loadingKeepSelection = true;
            KeepSelectionToggle.IsChecked = KeepSelectionAcrossViewsSettingsStore.Load();
            _loadingKeepSelection = false;

            _loadingDetailPanelPosition = true;
            DetailPanelPositionCombo.SelectedIndex = IndexForDetailPanelPosition(
                DetailPanelPositionSettingsStore.Load());
            _loadingDetailPanelPosition = false;

            _loadingDetailSectionPosition = true;
            DetailSectionPositionCombo.SelectedIndex = IndexForDetailSectionPosition(
                DetailSectionPositionSettingsStore.Load());
            _loadingDetailSectionPosition = false;

            _loadingTranslucentSidebar = true;
            TranslucentSidebarToggle.IsChecked = SidebarTranslucentSettingsStore.Load();
            _loadingTranslucentSidebar = false;

            _loadingTranslucentBackground = true;
            TranslucentBackgroundToggle.IsChecked = TranslucentBackgroundSettingsStore.Load();
            _loadingTranslucentBackground = false;

            LoadProfileEditor();
        };
    }

    private void LoadProfileEditor()
    {
        var profile = (DataContext as MainViewModel)?.UserProfile ?? UserProfileSettingsStore.Load();
        _profileState = ProfileEditorHelper.FromProfile(profile);
        ProfileDisplayNameBox.Text = profile.DisplayName;
        ProfileEditorHelper.PopulateDefaultAvatars(
            ProfileDefaultAvatarGrid,
            _profileState,
            RefreshProfilePreview,
            Application.Current.Resources);
        RefreshProfilePreview();
    }

    private void RefreshProfilePreview()
    {
        ProfileEditorHelper.RefreshPreview(
            ProfileAvatarPreview,
            ProfileEditorHelper.ToProfile(_profileState, ProfileDisplayNameBox.Text));
    }

    private void ProfileChoosePhoto_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = Strings.SetupChoosePhoto,
            Filter = Strings.SetupPhotoFilter
        };
        if (dialog.ShowDialog(Window.GetWindow(this)) != true)
            return;

        _profileState.CustomAvatarPath = UserProfileAvatarHelper.SaveCustomAvatar(dialog.FileName);
        _profileState.UseCustomAvatar = true;
        ProfileEditorHelper.SelectDefaultAvatar(
            ProfileDefaultAvatarGrid,
            _profileState.SelectedAvatarId,
            _profileState,
            Application.Current.Resources);
        foreach (var child in ProfileDefaultAvatarGrid.Children)
        {
            if (child is System.Windows.Controls.Button button)
                button.BorderBrush = Brushes.Transparent;
        }

        RefreshProfilePreview();
    }

    private void ProfileUseDefaultAvatar_Click(object sender, RoutedEventArgs e)
    {
        _profileState.UseCustomAvatar = false;
        _profileState.CustomAvatarPath = string.Empty;
        ProfileEditorHelper.SelectDefaultAvatar(
            ProfileDefaultAvatarGrid,
            _profileState.SelectedAvatarId,
            _profileState,
            Application.Current.Resources);
        RefreshProfilePreview();
    }

    private void SaveProfile_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(ProfileDisplayNameBox.Text))
        {
            MessageDialogWindow.ShowWarning(
                Strings.SetupDisplayNameRequired,
                Strings.SettingsProfileTitle,
                Window.GetWindow(this));
            ProfileDisplayNameBox.Focus();
            return;
        }

        var profile = ProfileEditorHelper.ToProfile(_profileState, ProfileDisplayNameBox.Text);
        UserProfileSettingsStore.Save(profile);
        if (DataContext is MainViewModel viewModel)
            viewModel.ApplyUserProfile(profile);

        MessageDialogWindow.Show(
            Strings.SettingsProfileSaved,
            Config.AppName,
            SymbolRegular.CheckmarkCircle24,
            Window.GetWindow(this));
    }

    private static int IndexForDetailPanelPosition(string position)
    {
        for (var i = 0; i < DetailPanelPositionValues.Length; i++)
        {
            if (DetailPanelPositionValues[i].Equals(position, StringComparison.OrdinalIgnoreCase))
                return i;
        }

        return 1;
    }

    private static int IndexForDetailSectionPosition(string position)
    {
        for (var i = 0; i < DetailSectionPositionValues.Length; i++)
        {
            if (DetailSectionPositionValues[i].Equals(position, StringComparison.OrdinalIgnoreCase))
                return i;
        }

        return 1;
    }

    private void ConfigureEmulator_Click(object sender, RoutedEventArgs e)
    {
        var owner = Window.GetWindow(this);
        var viewModel = App.Services.GetRequiredService<EmulationSettingsViewModel>();
        new EmulationSettingsWindow(viewModel) { Owner = owner }.ShowDialog();
    }

    private void IgdbSettings_Click(object sender, RoutedEventArgs e)
    {
        var owner = Window.GetWindow(this);
        var viewModel = App.Services.GetRequiredService<IgdbSettingsViewModel>();
        new IgdbSettingsWindow(viewModel) { Owner = owner }.ShowDialog();
    }

    private void SteamGridDbSettings_Click(object sender, RoutedEventArgs e)
    {
        var owner = Window.GetWindow(this);
        var viewModel = App.Services.GetRequiredService<SteamGridDbSettingsViewModel>();
        new SteamGridDbSettingsWindow(viewModel) { Owner = owner }.ShowDialog();
    }

    private void CustomThemeColor_Click(object sender, RoutedEventArgs e)
    {
        var owner = Window.GetWindow(this);
        new ThemeColorWindow { Owner = owner }.ShowDialog();
    }

    private void CreateBackup_Click(object sender, RoutedEventArgs e)
    {
        var owner = Window.GetWindow(this);
        var dialog = new SaveFileDialog
        {
            Title = Strings.BackupSaveDialogTitle,
            Filter = "Bridge backup (*.zip)|*.zip",
            FileName = $"Bridge-backup-{DateTime.Now:yyyy-MMdd-HHmm}.zip",
            DefaultExt = ".zip",
            AddExtension = true
        };

        if (dialog.ShowDialog(owner) != true)
            return;

        var result = AppDataBackupService.CreateBackup(dialog.FileName);
        if (result.Success)
        {
            MessageDialogWindow.Show(
                Strings.Format(nameof(Strings.BackupCreatedFormat), result.FilePath!),
                Config.AppName,
                SymbolRegular.CheckmarkCircle24,
                owner);
            return;
        }

        MessageDialogWindow.Show(
            Strings.Format(nameof(Strings.BackupFailedFormat), result.Message ?? Strings.Unknown),
            Config.AppName,
            SymbolRegular.ErrorCircle24,
            owner);
    }

    private void RestoreBackup_Click(object sender, RoutedEventArgs e)
    {
        var owner = Window.GetWindow(this);
        var dialog = new OpenFileDialog
        {
            Title = Strings.BackupRestoreDialogTitle,
            Filter = "Bridge backup (*.zip)|*.zip",
            CheckFileExists = true
        };

        if (dialog.ShowDialog(owner) != true)
            return;

        if (!MessageDialogWindow.ShowConfirm(
                Strings.BackupRestoreConfirm,
                Strings.RestoreBackup,
                SymbolRegular.Warning24,
                Strings.RestoreBackup,
                Strings.Cancel,
                owner))
        {
            return;
        }

        var result = AppDataBackupService.ScheduleRestore(dialog.FileName);
        if (!result.Success)
        {
            MessageDialogWindow.Show(
                Strings.Format(nameof(Strings.BackupRestoreFailedFormat), result.Message ?? Strings.Unknown),
                Config.AppName,
                SymbolRegular.ErrorCircle24,
                owner);
            return;
        }

        RestartApplication();
    }

    private static void RestartApplication()
    {
        var exe = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(exe))
        {
            Application.Current.Shutdown();
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = exe,
            UseShellExecute = true
        });
        Application.Current.Shutdown();
    }

    private void About_Click(object sender, RoutedEventArgs e)
    {
        var owner = Window.GetWindow(this);
        var background = (owner?.DataContext as MainViewModel)?.SelectedGame?.BackgroundImage;
        new AboutWindow(background) { Owner = owner }.ShowDialog();
    }

    private void BetaChannelToggle_Changed(object sender, RoutedEventArgs e)
    {
        UpdateChannelSettingsStore.Save(
            BetaChannelToggle.IsChecked == true ? UpdateChannel.Beta : UpdateChannel.Stable);
    }

    private void LanguageCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loadingLanguage || LanguageCombo.SelectedIndex < 0)
            return;

        var selected = LanguageCombo.SelectedIndex == 1
            ? AppLanguage.Spanish
            : AppLanguage.English;

        if (selected == LanguageSettingsStore.Load())
            return;

        LanguageSettingsStore.Save(selected);
        RestartApplication();
    }

    private void StartWithWindowsToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (_loadingStartWithWindows)
            return;

        var enabled = StartWithWindowsToggle.IsChecked == true;
        if (enabled == StartupSettingsStore.Load())
            return;

        if (!WindowsStartupRegistration.TrySetRegistered(enabled))
        {
            _loadingStartWithWindows = true;
            StartWithWindowsToggle.IsChecked = StartupSettingsStore.Load();
            _loadingStartWithWindows = false;

            var owner = Window.GetWindow(this);
            MessageDialogWindow.Show(
                Strings.SettingsStartWithWindowsFailed,
                Config.AppName,
                SymbolRegular.ErrorCircle24,
                owner);
            return;
        }

        StartupSettingsStore.Save(enabled);
    }

    private void TrayIconToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (_loadingTrayIcon)
            return;

        var enabled = TrayIconToggle.IsChecked == true;
        if (enabled == TrayIconSettingsStore.Load())
            return;

        TrayIconSettingsStore.Save(enabled);
        App.TrayIcon.Refresh();
    }

    private void KeepSelectionToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (_loadingKeepSelection)
            return;

        var enabled = KeepSelectionToggle.IsChecked == true;
        if (enabled == KeepSelectionAcrossViewsSettingsStore.Load())
            return;

        KeepSelectionAcrossViewsSettingsStore.Save(enabled);
    }

    private void DetailPanelPositionCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loadingDetailPanelPosition || DetailPanelPositionCombo.SelectedIndex < 0)
            return;

        var selected = DetailPanelPositionValues[DetailPanelPositionCombo.SelectedIndex];
        if (selected.Equals(DetailPanelPositionSettingsStore.Load(), StringComparison.OrdinalIgnoreCase))
            return;

        DetailPanelPositionSettingsStore.Save(selected);

        if (Window.GetWindow(this) is MainWindow mainWindow)
            mainWindow.LibraryDetail.ApplyDetailPanelPosition(selected);
    }

    private void DetailSectionPositionCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loadingDetailSectionPosition || DetailSectionPositionCombo.SelectedIndex < 0)
            return;

        var selected = DetailSectionPositionValues[DetailSectionPositionCombo.SelectedIndex];
        if (selected.Equals(DetailSectionPositionSettingsStore.Load(), StringComparison.OrdinalIgnoreCase))
            return;

        DetailSectionPositionSettingsStore.Save(selected);

        if (Window.GetWindow(this) is MainWindow mainWindow)
            mainWindow.LibraryDetail.ApplyDetailSectionPosition(selected);
    }

    private void TranslucentSidebarToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (_loadingTranslucentSidebar)
            return;

        var enabled = TranslucentSidebarToggle.IsChecked == true;
        if (enabled == SidebarTranslucentSettingsStore.Load())
            return;

        SidebarTranslucentSettingsStore.Save(enabled);
        ThemeManager.ApplySidebarAppearance();
    }

    private void TranslucentBackgroundToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (_loadingTranslucentBackground)
            return;

        var enabled = TranslucentBackgroundToggle.IsChecked == true;
        if (enabled == TranslucentBackgroundSettingsStore.Load())
            return;

        TranslucentBackgroundSettingsStore.Save(enabled);
        ThemeManager.ApplyTranslucentBackground();
    }
}
