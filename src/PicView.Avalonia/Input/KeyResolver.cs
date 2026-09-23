using Avalonia.Input;

namespace PicView.Avalonia.Input;

/// <summary>
/// Recovers the key for key events that arrive without a virtual-key code.
/// </summary>
/// <remarks>
/// Remote desktop clients in Unicode keyboard mode (e.g. Windows App / Microsoft Remote Desktop
/// on iPad) send printable characters as VK_PACKET, which Avalonia reports as <see cref="Key.None"/>.
/// Navigation keys still arrive as scancodes, so only letters, digits and similar are affected.
/// </remarks>
public static class KeyResolver
{
    public static Key Resolve(KeyEventArgs e)
    {
        if (e.Key != Key.None)
        {
            return e.Key;
        }

        if (e.PhysicalKey != PhysicalKey.None)
        {
            var fromPhysical = e.PhysicalKey.ToQwertyKey();
            if (fromPhysical != Key.None)
            {
                return fromPhysical;
            }
        }

        return FromSymbol(e.KeySymbol);
    }

    private static Key FromSymbol(string? symbol)
    {
        if (string.IsNullOrEmpty(symbol) || symbol.Length != 1)
        {
            return Key.None;
        }

        var c = char.ToUpperInvariant(symbol[0]);
        return c switch
        {
            >= 'A' and <= 'Z' => Key.A + (c - 'A'),
            >= '0' and <= '9' => Key.D0 + (c - '0'),
            ' ' => Key.Space,
            '+' or '=' => Key.OemPlus,
            '-' or '_' => Key.OemMinus,
            ',' or '<' => Key.OemComma,
            '.' or '>' => Key.OemPeriod,
            '/' or '?' => Key.OemQuestion,
            ';' or ':' => Key.OemSemicolon,
            '\'' or '"' => Key.OemQuotes,
            '[' or '{' => Key.OemOpenBrackets,
            ']' or '}' => Key.OemCloseBrackets,
            '\\' or '|' => Key.OemPipe,
            '`' or '~' => Key.OemTilde,
            _ => Key.None
        };
    }
}
