using Bridge.Services;

namespace Bridge.Tests.Services;

public class SetupWizardServiceTests : IDisposable
{
    private readonly string _setupPath = Config.SetupCompleteFilePath;
    private readonly string _whatsNewPath = Config.WhatsNewSeenFilePath;
    private readonly bool _hadSetup;
    private readonly string? _previousSetup;
    private readonly bool _hadWhatsNew;
    private readonly string? _previousWhatsNew;

    public SetupWizardServiceTests()
    {
        _hadSetup = File.Exists(_setupPath);
        _previousSetup = _hadSetup ? File.ReadAllText(_setupPath) : null;
        _hadWhatsNew = File.Exists(_whatsNewPath);
        _previousWhatsNew = _hadWhatsNew ? File.ReadAllText(_whatsNewPath) : null;
    }

    [Fact]
    public void ShouldShowSetup_ReturnsTrueForFreshInstall()
    {
        if (File.Exists(_setupPath))
            File.Delete(_setupPath);
        if (File.Exists(_whatsNewPath))
            File.Delete(_whatsNewPath);

        Assert.True(SetupWizardService.ShouldShowSetup());
    }

    [Fact]
    public void ShouldShowSetup_SkipsWhenWhatsNewSeenExists()
    {
        if (File.Exists(_setupPath))
            File.Delete(_setupPath);
        WhatsNewSettingsStore.Save(new Version(0, 4, 0));

        Assert.False(SetupWizardService.ShouldShowSetup());
        Assert.True(SetupCompleteSettingsStore.IsComplete());
    }

    public void Dispose()
    {
        RestoreFile(_setupPath, _hadSetup, _previousSetup);
        RestoreFile(_whatsNewPath, _hadWhatsNew, _previousWhatsNew);
    }

    private static void RestoreFile(string path, bool hadFile, string? previousContents)
    {
        if (hadFile && previousContents is not null)
            File.WriteAllText(path, previousContents);
        else if (File.Exists(path))
            File.Delete(path);
    }
}
