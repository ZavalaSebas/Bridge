using Bridge.Import.Epic;

namespace Bridge.Tests.Import;

public class EpicManifestLookupTests : IDisposable
{
    private readonly string _manifestsDir;

    public EpicManifestLookupTests()
    {
        _manifestsDir = Path.Combine(Path.GetTempPath(), $"bridge-epic-manifest-{Guid.NewGuid()}");
        Directory.CreateDirectory(_manifestsDir);
    }

    [Fact]
    public void TryGetSandboxId_ReturnsCatalogNamespaceFromManifest()
    {
        var appName = "051eaac0842c46d7a5a62858ad534d5a";
        var sandboxId = "c986e75258a146fba03a920dba852ca9";
        File.WriteAllText(
            Path.Combine(_manifestsDir, $"{appName}.item"),
            $$"""{"AppName":"{{appName}}","CatalogNamespace":"{{sandboxId}}"}""");

        var result = EpicManifestLookup.TryGetSandboxId(appName, _manifestsDir);

        Assert.Equal(sandboxId, result);
    }

    [Fact]
    public void TryGetSandboxId_ReturnsNullWhenManifestMissing()
    {
        Assert.Null(EpicManifestLookup.TryGetSandboxId("missing-app", _manifestsDir));
    }

    public void Dispose() => Directory.Delete(_manifestsDir, recursive: true);
}
