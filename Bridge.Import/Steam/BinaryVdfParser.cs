using System.Buffers.Binary;
using System.Text;

namespace Bridge.Import.Steam;

/// <summary>
/// Read-only parser for Valve binary KeyValues (UserGameStats*.bin under
/// Steam/appcache/stats). Returns the same shape as <see cref="VdfParser"/>:
/// string leaves and nested Dictionary&lt;string, object&gt; nodes.
/// </summary>
public static class BinaryVdfParser
{
    private const byte TypeSubKey = 0x00;
    private const byte TypeString = 0x01;
    private const byte TypeInt32 = 0x02;
    private const byte TypeFloat = 0x03;
    private const byte TypeUInt64 = 0x07;
    private const byte TypeEnd = 0x08;

    public static Dictionary<string, object> Parse(byte[] data) =>
        Parse(data.AsSpan());

    public static Dictionary<string, object> Parse(ReadOnlySpan<byte> data)
    {
        var (root, _) = ParseBlock(data, 0);
        return root;
    }

    private static (Dictionary<string, object> Block, int Position) ParseBlock(ReadOnlySpan<byte> data, int pos)
    {
        var result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        while (pos < data.Length)
        {
            var type = data[pos++];
            if (type == TypeEnd)
                return (result, pos);

            var (key, nextPos) = ReadCString(data, pos);
            pos = nextPos;

            object value;
            switch (type)
            {
                case TypeSubKey:
                {
                    var (child, childPos) = ParseBlock(data, pos);
                    value = child;
                    pos = childPos;
                    break;
                }
                case TypeString:
                {
                    var (text, textPos) = ReadCString(data, pos);
                    value = text;
                    pos = textPos;
                    break;
                }
                case TypeInt32:
                    value = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(pos, 4));
                    pos += 4;
                    break;
                case TypeFloat:
                    value = BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(data.Slice(pos, 4)));
                    pos += 4;
                    break;
                case TypeUInt64:
                    value = BinaryPrimitives.ReadUInt64LittleEndian(data.Slice(pos, 8));
                    pos += 8;
                    break;
                default:
                    return (result, pos);
            }

            result[key] = value;
        }

        return (result, pos);
    }

    private static (string Value, int Position) ReadCString(ReadOnlySpan<byte> data, int pos)
    {
        var start = pos;
        while (pos < data.Length && data[pos] != 0)
            pos++;

        var value = pos > start
            ? Encoding.UTF8.GetString(data.Slice(start, pos - start))
            : string.Empty;

        if (pos < data.Length)
            pos++;

        return (value, pos);
    }
}
