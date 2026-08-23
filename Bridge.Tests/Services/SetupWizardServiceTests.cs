using Bridge.Services;

namespace Bridge.Tests.Services;

public class SetupWizardServiceTests : IDisposable
{
    private readonly TrackedFile _setup = Track(Config.SetupCompleteFilePath, Path.Combine(Config.AppDataPath, "setup-complete.txt"));
    private readonly TrackedFile _whatsNew = Track(Config.WhatsNewSeenFilePath, Path.Combine(Config.AppDataPath, "whats-new-seen.txt"));
    private readonly TrackedFile _romScanFolder = Track(Config.RomScanFolderFilePath, Path.Combine(Config.AppDataPath, "rom-scan-folder.txt"));
    private readonly TrackedFile _installedScanFolder = Track(Config.InstalledScanFolderFilePath, Path.Combine(Config.AppDataPath, "installed-scan-folder.txt"));

    [Fact]
    public void ShouldShowSetup_ReturnsTrueForFreshInstall()
    {
        ClearSetupState();

        Assert.True(SetupWizardService.ShouldShowSetup());
    }

    [Fact]
    public void ShouldShowSetup_SkipsWhenWhatsNewSeenExists()
    {
        ClearSetupState();
        WhatsNewSettingsStore.Save(new Version(0, 4, 0));

        Assert.False(SetupWizardService.ShouldShowSetup());
        Assert.True(SetupCompleteSettingsStore.IsComplete());
    }

    public void Dispose()
    {
        _setup.Restore();
        _whatsNew.Restore();
        _romScanFolder.Restore();
        _installedScanFolder.Restore();
    }

    private void ClearSetupState()
    {
        _setup.DeleteBoth();
        _whatsNew.DeleteBoth();
        _romScanFolder.DeleteBoth();
        _installedScanFolder.DeleteBoth();
    }

    private static TrackedFile Track(string currentPath, string legacyPath) => new(currentPath, legacyPath);

    private sealed class TrackedFile(string currentPath, string legacyPath)
    {
        private readonly bool _hadCurrent = File.Exists(currentPath);
        private readonly string? _previousCurrent = File.Exists(currentPath) ? File.ReadAllText(currentPath) : null;
        private readonly bool _hadLegacy = File.Exists(legacyPath);
        private readonly string? _previousLegacy = File.Exists(legacyPath) ? File.ReadAllText(legacyPath) : null;

        public void DeleteBoth()
        {
            DeleteIfExists(currentPath);
            DeleteIfExists(legacyPath);
        }

        public void Restore()
        {
            RestorePath(currentPath, _hadCurrent, _previousCurrent);
            RestorePath(legacyPath, _hadLegacy, _previousLegacy);
        }

        private static void DeleteIfExists(string path)
        {
            if (File.Exists(path))
                File.Delete(path);
        }

        private static void RestorePath(string path, bool hadFile, string? previousContents)
        {
            if (hadFile && previousContents is not null)
                File.WriteAllText(path, previousContents);
            else if (File.Exists(path))
                File.Delete(path);
        }
    }
}
