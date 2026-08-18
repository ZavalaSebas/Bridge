using System.Resources;

namespace Bridge.Resources;

internal static class StringsResourceManager
{
    private static readonly ResourceManager Manager = new(
        "Bridge.Resources.Strings",
        typeof(StringsResourceManager).Assembly);

    public static string? GetString(string name) => Manager.GetString(name);
}
