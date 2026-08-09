using System.Text;

namespace Bridge.Import.Steam;

/// <summary>
/// Minimal reader for Valve's VDF (KeyValue) text format — used by both
/// libraryfolders.vdf and appmanifest*.acf (verified against real files on
/// this machine, and against Playnite's real SteamLibrary extension source,
/// PROJECT_FOUNDATION.md §28.26, which uses SteamKit2's KeyValue class for
/// the same job). This is a hand-rolled, read-only subset — just enough to
/// parse the two file shapes Bridge actually needs, not a general VDF
/// writer/editor. Format: nested `"key" "value"` pairs and `"key" { ... }`
/// blocks, `//` line comments, backslash-escaped characters inside quotes
/// (paths use `\\` for a literal backslash).
///
/// A node is either a leaf `string` or a nested `Dictionary&lt;string, object&gt;`
/// — callers pattern-match on which one they got via `is string` / `is
/// Dictionary&lt;string, object&gt;`.
/// </summary>
public static class VdfParser
{
    public static Dictionary<string, object> Parse(string content)
    {
        var tokens = Tokenize(content);
        var pos = 0;
        return ParseBlock(tokens, ref pos);
    }

    private static List<string> Tokenize(string content)
    {
        var tokens = new List<string>();
        var i = 0;
        while (i < content.Length)
        {
            var c = content[i];

            if (char.IsWhiteSpace(c))
            {
                i++;
                continue;
            }

            if (c == '/' && i + 1 < content.Length && content[i + 1] == '/')
            {
                while (i < content.Length && content[i] != '\n')
                {
                    i++;
                }
                continue;
            }

            if (c is '{' or '}')
            {
                tokens.Add(c.ToString());
                i++;
                continue;
            }

            if (c == '"')
            {
                i++;
                var sb = new StringBuilder();
                while (i < content.Length && content[i] != '"')
                {
                    if (content[i] == '\\' && i + 1 < content.Length)
                    {
                        sb.Append(content[i + 1]);
                        i += 2;
                        continue;
                    }

                    sb.Append(content[i]);
                    i++;
                }

                i++;
                tokens.Add(sb.ToString());
                continue;
            }

            // Unexpected/unquoted character outside a string — skip it rather
            // than throw. These files are Valve-generated, not user input;
            // being lenient here matters more than being strict.
            i++;
        }

        return tokens;
    }

    private static Dictionary<string, object> ParseBlock(List<string> tokens, ref int pos)
    {
        var result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        while (pos < tokens.Count)
        {
            var token = tokens[pos];
            if (token == "}")
            {
                pos++;
                return result;
            }

            var key = token;
            pos++;
            if (pos >= tokens.Count)
            {
                break;
            }

            if (tokens[pos] == "{")
            {
                pos++;
                result[key] = ParseBlock(tokens, ref pos);
            }
            else
            {
                result[key] = tokens[pos];
                pos++;
            }
        }

        return result;
    }
}
