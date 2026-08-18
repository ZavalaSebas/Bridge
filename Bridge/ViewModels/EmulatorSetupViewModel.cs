using Bridge.Core.Contracts;
using Bridge.Core.Entities;
using Bridge.Resources;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Bridge.ViewModels;

/// <summary>Single-emulator setup screen — one Emulator with one profile.</summary>
public partial class EmulatorSetupViewModel : ObservableObject
{
    private readonly IRepository<Emulator> _emulatorRepository;
    private Emulator? _existing;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _installDirectory = string.Empty;

    [ObservableProperty]
    private string _executable = string.Empty;

    [ObservableProperty]
    private string _arguments = "{RomPath}";

    [ObservableProperty]
    private string _extensions = string.Empty;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    public event Action? Saved;

    public EmulatorSetupViewModel(IRepository<Emulator> emulatorRepository)
    {
        _emulatorRepository = emulatorRepository;

        _existing = _emulatorRepository.GetAll().FirstOrDefault();
        if (_existing is not null)
        {
            Name = _existing.Name;
            InstallDirectory = _existing.InstallDirectory;
            var profile = _existing.Profiles.FirstOrDefault();
            if (profile is not null)
            {
                Executable = profile.Executable;
                Arguments = profile.Arguments;
                Extensions = string.Join(", ", profile.ImageExtensions);
            }
        }
    }

    [RelayCommand]
    private void Save()
    {
        if (string.IsNullOrWhiteSpace(Name) || string.IsNullOrWhiteSpace(Executable))
        {
            StatusMessage = Strings.EmulatorNameExecutableRequired;
            return;
        }

        var extensions = Extensions
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

        var emulator = _existing ?? new Emulator();
        emulator.Name = Name.Trim();
        emulator.InstallDirectory = InstallDirectory.Trim();

        var profile = emulator.Profiles.FirstOrDefault() ?? new EmulatorProfile { Id = Guid.NewGuid().ToString() };
        profile.Name = "Default";
        profile.Executable = Executable.Trim();
        profile.Arguments = Arguments.Trim();
        profile.ImageExtensions = extensions;

        if (!emulator.Profiles.Contains(profile))
        {
            emulator.Profiles.Add(profile);
        }

        if (_existing is null)
        {
            _emulatorRepository.Add(emulator);
            _existing = emulator;
        }
        else
        {
            _emulatorRepository.Update(emulator);
        }

        StatusMessage = Strings.Format(nameof(Strings.EmulatorSavedFormat), emulator.Name);
        Saved?.Invoke();
    }
}
