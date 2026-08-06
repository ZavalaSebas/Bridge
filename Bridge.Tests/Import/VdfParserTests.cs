using Bridge.Import.Steam;

namespace Bridge.Tests.Import;

public class VdfParserTests
{
    [Fact]
    public void Parse_SimpleKeyValue()
    {
        var result = VdfParser.Parse("""
            "AppState"
            {
                "appid"     "12345"
                "name"      "Test Game"
            }
            """);

        var appState = Assert.IsType<Dictionary<string, object>>(result["AppState"]);
        Assert.Equal("12345", appState["appid"]);
        Assert.Equal("Test Game", appState["name"]);
    }

    [Fact]
    public void Parse_NestedBlocks()
    {
        var result = VdfParser.Parse("""
            "libraryfolders"
            {
                "0"
                {
                    "path"  "C:\\Steam"
                    "apps"
                    {
                        "100"   "0"
                    }
                }
            }
            """);

        var root = Assert.IsType<Dictionary<string, object>>(result["libraryfolders"]);
        var zero = Assert.IsType<Dictionary<string, object>>(root["0"]);
        Assert.Equal(@"C:\Steam", zero["path"]);
        var apps = Assert.IsType<Dictionary<string, object>>(zero["apps"]);
        Assert.Equal("0", apps["100"]);
    }

    [Fact]
    public void Parse_HandlesEscapedBackslashesInPaths()
    {
        var result = VdfParser.Parse("""
            "AppState"
            {
                "path" "D:\\Games\\My Game"
            }
            """);

        var appState = Assert.IsType<Dictionary<string, object>>(result["AppState"]);
        Assert.Equal(@"D:\Games\My Game", appState["path"]);
    }

    [Fact]
    public void Parse_IgnoresLineComments()
    {
        var result = VdfParser.Parse("""
            // this is a comment
            "AppState"
            {
                "appid" "1" // trailing comment
            }
            """);

        var appState = Assert.IsType<Dictionary<string, object>>(result["AppState"]);
        Assert.Equal("1", appState["appid"]);
    }

    [Fact]
    public void Parse_EmptyContent_ReturnsEmptyDictionary()
    {
        var result = VdfParser.Parse("");
        Assert.Empty(result);
    }
}
