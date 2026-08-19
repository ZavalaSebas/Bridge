using System.Globalization;
using Bridge.Resources;
using Bridge.Services;

namespace Bridge.Tests.Services;

public class LanguageSettingsStoreTests
{
    [Fact]
    public void CultureFor_Spanish_UsesEsCulture()
    {
        var culture = LanguageSettingsStore.CultureFor(AppLanguage.Spanish);
        Assert.Equal("es", culture.Name);
    }

    [Fact]
    public void SpanishResources_ResolveKnownString()
    {
        var previous = StringsResourceManager.Culture;
        try
        {
            StringsResourceManager.Culture = CultureInfo.GetCultureInfo("es");
            Assert.Equal("Cancelar", Strings.Cancel);
            Assert.Equal("Configuración", Strings.Settings);
        }
        finally
        {
            StringsResourceManager.Culture = previous;
        }
    }
}
