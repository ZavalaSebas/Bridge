using System.Windows;

namespace Bridge.Services;

public static class WhatsNewService
{
    public static bool ShouldShowWhatsNew(out WhatsNewRelease? release)
    {
        release = null;
        var current = WhatsNewSettingsStore.Normalize(Config.AssemblyVersion);
        var lastSeen = WhatsNewSettingsStore.Load();

        if (lastSeen is null)
        {
            WhatsNewSettingsStore.Save(current);
            return false;
        }

        if (current <= lastSeen)
            return false;

        if (!WhatsNewParser.TryGetCurrentReleaseNotes(out var parsed))
        {
            WhatsNewSettingsStore.Save(current);
            return false;
        }

        release = parsed;
        return true;
    }

    public static void ShowIfNeeded(Window owner)
    {
        if (!ShouldShowWhatsNew(out var release) || release is null)
            return;

        var window = new WhatsNewWindow(release) { Owner = owner };
        window.ShowDialog();
        WhatsNewSettingsStore.Save(release.Version);
    }
}
