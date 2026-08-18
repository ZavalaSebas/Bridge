using System.Text;

namespace Bridge.Import.Steam;

/// <summary>
/// Read-only parser for Valve VDF text (libraryfolders.vdf, appmanifest*.acf):
/// nested "key" "value" pairs and "key" { ... } blocks. A node is either a
/// string leaf or a Dictionary&lt;string, object&gt;.
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
