using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Bridge.Core.Enums;
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
    private bool _loadingCoversDetailLayout;
    private bool _loadingDetailSectionPosition;
    private bool _loadingTranslucentSidebar;
    private bool _loadingTranslucentBackground;
    private bool _loadingDetailHeroButtons;
    private bool _loadingDetailSideButtons;
    private bool _loadingStartupSection;
    private bool _loadingMinimizeOnGameLaunch;
    private bool _loadingFont;
    private bool _loadingRomSaveAutoBackup;
    private bool _loadingPcSaveAutoBackup;
    private bool _loadingRomOrganize;
    private ProfileEditorHelper.AvatarEditorState _profileState = new();

    private static readonly string[] DetailPanelPositionValues =
    [
        DetailPanelPositionSettingsStore.Left,
        DetailPanelPositionSettingsStore.Right
    ];

    private static readonly string[] CoversDetailLayoutValues =
    [
        CoversDetailLayoutSettingsStore.Compact,
        CoversDetailLayoutSettingsStore.Standard
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

            _loadingCoversDetailLayout = true;
            CoversDetailLayoutCombo.SelectedIndex = IndexForCoversDetailLayout(
                CoversDetailLayoutSettingsStore.Load());
            _loadingCoversDetailLayout = false;

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

            _loadingDetailHeroButtons = true;
            DetailHeroButtonsToggle.IsChecked = DetailHeroButtonsSettingsStore.Load();
            _loadingDetailHeroButtons = false;

            _loadingDetailSideButtons = true;
            DetailSideButtonsToggle.IsChecked = DetailSideButtonsSettingsStore.Load();
            _loadingDetailSideButtons = false;

            _loadingStartupSection = true;
            StartupSectionCombo.SelectedIndex = IndexForStartupSection(StartupSectionSettingsStore.Load());
            _loadingStartupSection = false;

            _loadingMinimizeOnGameLaunch = true;
            MinimizeOnGameLaunchToggle.IsChecked = MinimizeOnGameLaunchSettingsStore.Load();
            _loadingMinimizeOnGameLaunch = false;

            _loadingFont = true;
            FontFamilyCombo.SelectedIndex = (int)FontSettingsStore.Load();
            _loadingFont = false;

            _loadingRomSaveAutoBackup = true;
            RomSaveAutoBackupToggle.IsChecked = RomSaveAutoBackupSettingsStore.Load();
            _loadingRomSaveAutoBackup = false;

            _loadingPcSaveAutoBackup = true;
            PcSaveAutoBackupToggle.IsChecked = PcSaveAutoBackupSettingsStore.Load();
            _loadingPcSaveAutoBackup = false;

            _loadingRomOrganize = true;
            RomOrganizeToggle.IsChecked = RomOrganizeSettingsStore.Load();
            _loadingRomOrganize = false;

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

    private void RetroAchievementsSettings_Click(object sender, RoutedEventArgs e)
    {
        var owner = Window.GetWindow(this);
        var viewModel = App.Services.GetRequiredService<RetroAchievementsSettingsViewModel>();
        new RetroAchievementsSettingsWindow(viewModel) { Owner = owner }.ShowDialog();
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
            Filter = Strings.BackupFileFilter,
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
            Filter = Strings.BackupFileFilter,
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

    private void CoversDetailLayoutCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loadingCoversDetailLayout || CoversDetailLayoutCombo.SelectedIndex < 0)
            return;

        var selected = CoversDetailLayoutValues[CoversDetailLayoutCombo.SelectedIndex];
        if (selected.Equals(CoversDetailLayoutSettingsStore.Load(), StringComparison.OrdinalIgnoreCase))
            return;

        CoversDetailLayoutSettingsStore.Save(selected);

        if (Window.GetWindow(this) is MainWindow mainWindow)
            mainWindow.LibraryDetail.ApplyDetailPanelPosition();
    }

    private static int IndexForCoversDetailLayout(string layout)
    {
        for (var i = 0; i < CoversDetailLayoutValues.Length; i++)
        {
            if (CoversDetailLayoutValues[i].Equals(layout, StringComparison.OrdinalIgnoreCase))
                return i;
        }

        return 0;
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

    private void DetailHeroButtonsToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (_loadingDetailHeroButtons)
            return;

        var enabled = DetailHeroButtonsToggle.IsChecked == true;
        if (enabled == DetailHeroButtonsSettingsStore.Load())
            return;

        DetailHeroButtonsSettingsStore.Save(enabled);
        if (DataContext is MainViewModel vm)
            vm.ShowDetailHeroButtons = enabled;
    }

    private void DetailSideButtonsToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (_loadingDetailSideButtons)
            return;

        var enabled = DetailSideButtonsToggle.IsChecked == true;
        if (enabled == DetailSideButtonsSettingsStore.Load())
            return;

        DetailSideButtonsSettingsStore.Save(enabled);
        if (DataContext is MainViewModel vm)
            vm.ShowDetailSideButtons = enabled;
    }

    private void MinimizeOnGameLaunchToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (_loadingMinimizeOnGameLaunch) return;
        var enabled = MinimizeOnGameLaunchToggle.IsChecked == true;
        if (enabled == MinimizeOnGameLaunchSettingsStore.Load()) return;
        MinimizeOnGameLaunchSettingsStore.Save(enabled);
    }

    private static readonly NavigationSection[] StartupSectionValues =
    [
        NavigationSection.Home,
        NavigationSection.Library,
        NavigationSection.Roms
    ];

    private static int IndexForStartupSection(NavigationSection section)
    {
        for (var i = 0; i < StartupSectionValues.Length; i++)
            if (StartupSectionValues[i] == section) return i;
        return 1;
    }

    private void StartupSectionCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loadingStartupSection || StartupSectionCombo.SelectedIndex < 0) return;
        var selected = StartupSectionValues[StartupSectionCombo.SelectedIndex];
        if (selected == StartupSectionSettingsStore.Load()) return;
        StartupSectionSettingsStore.Save(selected);
    }

    private void FontFamilyCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loadingFont || FontFamilyCombo.SelectedIndex < 0) return;
        var selected = (AppFont)FontFamilyCombo.SelectedIndex;
        if (selected == FontSettingsStore.Load()) return;
        FontManager.Apply(selected);
    }

    private void DeleteAllData_Click(object sender, RoutedEventArgs e)
    {
        var owner = Window.GetWindow(this);
        // 1st verification
        if (!MessageDialogWindow.ShowConfirm(
                Strings.DeleteAllDataConfirm1Message,
                Strings.DeleteAllDataConfirm1Title,
                SymbolRegular.Warning24,
                Strings.DeleteAllDataButton,
                Strings.Cancel,
                owner))
            return;

        // 2nd verification - type BORRAR + checkbox
        var win2 = new DeleteConfirmWindow { Owner = owner };
        if (win2.ShowDialog() != true || !win2.Confirmed)
            return;

        // 3rd verification
        if (!MessageDialogWindow.ShowConfirm(
                Strings.DeleteAllDataConfirm3Message,
                Strings.DeleteAllDataConfirm3Title,
                SymbolRegular.Warning24,
                Strings.DeleteAllDataButton,
                Strings.Cancel,
                owner))
            return;

        Mouse.OverrideCursor = Cursors.Wait;
        try
        {
            var result = AppDataPurgeService.PurgeAllData();
            if (!result.Success)
            {
                MessageDialogWindow.Show(
                    string.Format(Strings.DeleteAllDataFailedFormat, result.Message ?? Strings.Unknown),
                    Config.AppName,
                    SymbolRegular.ErrorCircle24,
                    owner);
                return;
            }

            MessageDialogWindow.Show(
                Strings.DeleteAllDataSuccess,
                Config.AppName,
                SymbolRegular.CheckmarkCircle24,
                owner);
            RestartApplication();
        }
        finally
        {
            Mouse.OverrideCursor = null;
        }
    }

    private void RomSaveAutoBackupToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (_loadingRomSaveAutoBackup)
            return;

        var enabled = RomSaveAutoBackupToggle.IsChecked == true;
        if (enabled == RomSaveAutoBackupSettingsStore.Load())
            return;

        RomSaveAutoBackupSettingsStore.Save(enabled);
    }

    private void PcSaveAutoBackupToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (_loadingPcSaveAutoBackup)
            return;

        var enabled = PcSaveAutoBackupToggle.IsChecked == true;
        if (enabled == PcSaveAutoBackupSettingsStore.Load())
            return;

        PcSaveAutoBackupSettingsStore.Save(enabled);
    }

    private async void ExportRomPack_Click(object sender, RoutedEventArgs e)
    {
        var owner = Window.GetWindow(this);
        if (DataContext is not MainViewModel viewModel)
            return;

        var dialog = new SaveFileDialog
        {
            Title = Strings.RomPackSaveDialogTitle,
            Filter = Strings.RomPackFileFilter,
            FileName = $"Bridge-roms-{DateTime.Now:yyyy-MMdd-HHmm}.zip",
            DefaultExt = ".zip",
            AddExtension = true
        };

        if (dialog.ShowDialog(owner) != true)
            return;

        var games = viewModel.Games.ToList();
        Mouse.OverrideCursor = Cursors.Wait;
        RomLibraryPackResult result;
        try
        {
            result = await Task.Run(() => RomLibraryPackService.Create(
                games,
                dialog.FileName,
                customSaveFolders: GameSaveFolderStore.GetAll()));
        }
        finally
        {
            Mouse.OverrideCursor = null;
        }

        if (result.Success)
        {
            MessageDialogWindow.Show(
                Strings.Format(
                    nameof(Strings.RomPackCreatedFormat),
                    result.FilePath!,
                    result.GamesWithSaves,
                    result.RomsIncluded,
                    result.RomsSkipped),
                Config.AppName,
                SymbolRegular.CheckmarkCircle24,
                owner);
            return;
        }

        MessageDialogWindow.Show(
            Strings.Format(nameof(Strings.RomPackFailedFormat), result.Message ?? Strings.Unknown),
            Config.AppName,
            SymbolRegular.ErrorCircle24,
            owner);
    }

    private async void ImportRomPack_Click(object sender, RoutedEventArgs e)
    {
        var owner = Window.GetWindow(this);
        if (DataContext is not MainViewModel viewModel)
            return;

        var dialog = new OpenFileDialog
        {
            Title = Strings.RomPackRestoreDialogTitle,
            Filter = Strings.RomPackFileFilter,
            CheckFileExists = true
        };

        if (dialog.ShowDialog(owner) != true)
            return;

        if (!MessageDialogWindow.ShowConfirm(
                Strings.RomPackRestoreConfirm,
                Strings.ImportRomPack,
                SymbolRegular.ArrowImport24,
                Strings.ImportRomPack,
                Strings.Cancel,
                owner))
        {
            return;
        }

        var romFolder = RomScanFolderSettingsStore.Load();
        if (string.IsNullOrWhiteSpace(romFolder) || !Directory.Exists(romFolder))
            romFolder = Config.ImportedRomsPath;

        Mouse.OverrideCursor = Cursors.Wait;
        RomLibraryPackResult result;
        try
        {
            result = await Task.Run(() => RomLibraryPackService.Import(dialog.FileName, romFolder));
        }
        finally
        {
            Mouse.OverrideCursor = null;
        }

        if (!result.Success)
        {
            MessageDialogWindow.Show(
                Strings.Format(nameof(Strings.RomPackImportFailedFormat), result.Message ?? Strings.Unknown),
                Config.AppName,
                SymbolRegular.ErrorCircle24,
                owner);
            return;
        }

        if (result.RestoredSaveFolders is { Count: > 0 })
        {
            foreach (var (gameId, folder) in result.RestoredSaveFolders)
                GameSaveFolderStore.Set(gameId, folder);

            viewModel.NotifySaveFolderBindings();
        }

        if (result.RomsCopied > 0)
        {
            foreach (var folder in result.RomDestinations ?? [])
            {
                if (!string.IsNullOrWhiteSpace(folder))
                    await viewModel.ScanRomFolderAsync(folder, silent: true, persistWatchedFolder: false);
            }
        }

        viewModel.RefreshSelectedGameSaveBackups();

        MessageDialogWindow.Show(
            Strings.Format(
                nameof(Strings.RomPackImportedFormat),
                result.SavesRestored,
                result.RomsCopied),
            Config.AppName,
            SymbolRegular.CheckmarkCircle24,
            owner);
    }

    private void RomOrganizeToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (_loadingRomOrganize)
            return;

        var enabled = RomOrganizeToggle.IsChecked == true;
        if (enabled == RomOrganizeSettingsStore.Load())
            return;

        RomOrganizeSettingsStore.Save(enabled);
    }

    private async void OrganizeRomsNow_Click(object sender, RoutedEventArgs e)
    {
        var owner = Window.GetWindow(this);
        if (DataContext is not MainViewModel viewModel)
            return;

        var folder = RomScanFolderSettingsStore.Load();
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
        {
            MessageDialogWindow.Show(
                Strings.OrganizeRomsNoFolder,
                Strings.SettingsOrganizeRomsTitle,
                SymbolRegular.Folder24,
                owner);
            return;
        }

        Mouse.OverrideCursor = Cursors.Wait;
        Bridge.Emulation.RomOrganizeResult result;
        try
        {
            result = await viewModel.OrganizeRomsNowAsync();
        }
        finally
        {
            Mouse.OverrideCursor = null;
        }

        MessageDialogWindow.Show(
            Strings.Format(
                nameof(Strings.OrganizeRomsCompleteFormat),
                result.Changes.Count,
                result.Unchanged,
                result.Skipped + result.Failed),
            Strings.SettingsOrganizeRomsTitle,
            SymbolRegular.CheckmarkCircle24,
            owner);
    }
}
