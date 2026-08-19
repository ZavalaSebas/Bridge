using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
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
        };
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
}
