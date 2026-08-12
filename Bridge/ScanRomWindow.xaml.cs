using System.Windows;
using System.Windows.Controls;
using Bridge.Core.Entities;
using Bridge.Core.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;

namespace Bridge;

public partial class ScanRomWindow : Window
{
    private readonly IRepository<Emulator> _emulatorRepository;
    private Emulator? _emulator;
    private EmulatorProfile? _profile;

    public string RomFolder => FolderBox.Text.Trim();
    public Guid EmulatorId => _emulator?.Id ?? Guid.Empty;
    public string EmulatorProfileId => _profile?.Id ?? string.Empty;

    public ScanRomWindow()
    {
        // Services before InitializeComponent (no bound events here, but keep the
        // pattern consistent with ScanInstalledWindow).
        var services = App.Services;
        _emulatorRepository = services.GetRequiredService<IRepository<Emulator>>();
        InitializeComponent();

        Loaded += ScanRomWindow_Loaded;
    }

    private void ScanRomWindow_Loaded(object sender, RoutedEventArgs e)
    {
        var emulators = _emulatorRepository.GetAll();
        if (emulators.Count == 0)
        {
            EmulatorBox.Items.Add("No emulators configured — configure one first.");
            EmulatorBox.IsEnabled = false;
            ProfileBox.IsEnabled = false;
            return;
        }

        EmulatorBox.DisplayMemberPath = nameof(Emulator.Name);
        EmulatorBox.ItemsSource = emulators;
        EmulatorBox.SelectedIndex = 0;
    }

    private void EmulatorBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _emulator = EmulatorBox.SelectedItem as Emulator;
        RefreshProfiles();
    }

    private void RefreshProfiles()
    {
        _profile = null;
        if (_emulator is null || _emulator.Profiles.Count == 0)
        {
            ProfileBox.ItemsSource = null;
            ProfileBox.IsEnabled = false;
            ExtensionsText.Text = _emulator is null
                ? string.Empty
                : "This emulator has no profiles — add one before scanning.";
            return;
        }

        ProfileBox.DisplayMemberPath = nameof(EmulatorProfile.Name);
        ProfileBox.ItemsSource = _emulator.Profiles;
        ProfileBox.SelectedIndex = 0;
        ProfileBox.IsEnabled = true;
    }

    private void ProfileBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _profile = ProfileBox.SelectedItem as EmulatorProfile;
        if (_profile is not null && _profile.ImageExtensions.Count > 0)
        {
            ExtensionsText.Text = $"Extensions: {string.Join(", ", _profile.ImageExtensions)}";
        }
        else
        {
            ExtensionsText.Text = _profile is null ? string.Empty : "This profile has no extensions — everything will be scanned.";
        }
    }

    private void BrowseFolder_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "Select ROM folder" };
        if (dialog.ShowDialog(this) == true)
        {
            FolderBox.Text = dialog.FolderName;
        }
    }

    private void Scan_Click(object sender, RoutedEventArgs e)
    {
        if (_emulator is null || _profile is null)
        {
            MessageBox.Show(this, "Select an emulator and profile first.", "Scan ROMs", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(FolderBox.Text))
        {
            MessageBox.Show(this, "Select a folder to scan.", "Scan ROMs", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        DialogResult = true;
    }
}
