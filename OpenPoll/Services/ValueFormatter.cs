using System;
using System.Globalization;
using System.Text.RegularExpressions;
using OpenPoll.Models;

namespace OpenPoll.Services;

public static class ValueFormatter
{
    // ─────────────────────────────────────────────────────────────────
    //  Format
    // ─────────────────────────────────────────────────────────────────

    public static string FormatRegister(int raw, CellDataType type) =>
        FormatRegister(new[] { raw }, type, WordOrder.BigEndian);

    public static string FormatRegister(int[] words, CellDataType type, WordOrder order)
    {
        if (words.Length == 0) return "";

        switch (type)
        {
            // 16-bit (1 word)
            case CellDataType.Signed:   return ((short)words[0]).ToString();
            case CellDataType.Unsigned: return ((ushort)words[0]).ToString();
            case CellDataType.Hex:      return "0x" + ((ushort)words[0]).ToString("X4");
            case CellDataType.Binary:   return FormatBinary16((ushort)words[0]);

            // 32-bit (2 words)
            case CellDataType.Signed32 when words.Length >= 2:
                return ((int)Combine32(words, order)).ToString();
            case CellDataType.Unsigned32 when words.Length >= 2:
                return Combine32(words, order).ToString();
            case CellDataType.Hex32 when words.Length >= 2:
                return "0x" + Combine32(words, order).ToString("X8");
            case CellDataType.Float when words.Length >= 2:
                // G9 is the minimum precision that round-trips a 32-bit float.
                return BitConverter.Int32BitsToSingle((int)Combine32(words, order)).ToString("G9", CultureInfo.InvariantCulture);

            // 64-bit (4 words)
            case CellDataType.Signed64 when words.Length >= 4:
                return ((long)Combine64(words, order)).ToString();
            case CellDataType.Unsigned64 when words.Length >= 4:
                return Combine64(words, order).ToString();
            case CellDataType.Hex64 when words.Length >= 4:
                return "0x" + Combine64(words, order).ToString("X16");
            case CellDataType.Double when words.Length >= 4:
                // G17 is the minimum precision that round-trips a 64-bit double.
                return BitConverter.Int64BitsToDouble((long)Combine64(words, order)).ToString("G17", CultureInfo.InvariantCulture);
        }
        return words[0].ToString();
    }

    public static string FormatCoil(bool value) => value ? "1" : "0";

    // ─────────────────────────────────────────────────────────────────
    //  Parse
    // ─────────────────────────────────────────────────────────────────

    /// <summary>Parses text for a 16-bit destination. For multi-register types, the caller
    /// must split the value across multiple consecutive registers.</summary>
    public static bool TryParseRegister(string text, CellDataType type, out int raw)
    {
        raw = 0;
        if (string.IsNullOrWhiteSpace(text)) return false;
        var trimmed = text.Trim();

        switch (type)
        {
            case CellDataType.Signed:
                if (short.TryParse(trimmed, out var s)) { raw = (ushort)s; return true; }
                return false;

            case CellDataType.Unsigned:
                if (ushort.TryParse(trimmed, out var u)) { raw = u; return true; }
                return false;

            case CellDataType.Hex:
                var hex = trimmed.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? trimmed[2..] : trimmed;
                if (ushort.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var h))
                {
                    raw = h; return true;
                }
                return false;

            case CellDataType.Binary:
                var stripped = trimmed.Replace(" ", "");
                if (stripped.Length is > 0 and <= 16 && Regex.IsMatch(stripped, "^[01]+$"))
                {
                    raw = Convert.ToUInt16(stripped, 2); return true;
                }
                return false;

            // Multi-register types: caller should use TryParseMultiRegister instead
            default:
                return false;
        }
    }

    /// <summary>Parses text for a multi-register destination. Returns the raw words to write
    /// in protocol order, respecting the word/byte order.</summary>
    public static bool TryParseMultiRegister(string text, CellDataType type, WordOrder order, out int[] words)
    {
        words = Array.Empty<int>();
        if (string.IsNullOrWhiteSpace(text)) return false;
        var t = text.Trim();

        switch (type)
        {
            case CellDataType.Signed32:
                if (int.TryParse(t, out var i32)) { words = Split32(unchecked((uint)i32), order); return true; }
                return false;
            case CellDataType.Unsigned32:
                if (uint.TryParse(t, out var u32)) { words = Split32(u32, order); return true; }
                return false;
            case CellDataType.Hex32:
                var h32 = t.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? t[2..] : t;
                if (uint.TryParse(h32, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var hu32))
                {
                    words = Split32(hu32, order); return true;
                }
                return false;
            case CellDataType.Float:
                if (float.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out var f))
                {
                    words = Split32(unchecked((uint)BitConverter.SingleToInt32Bits(f)), order); return true;
                }
                return false;

            case CellDataType.Signed64:
                if (long.TryParse(t, out var i64)) { words = Split64(unchecked((ulong)i64), order); return true; }
                return false;
            case CellDataType.Unsigned64:
                if (ulong.TryParse(t, out var u64)) { words = Split64(u64, order); return true; }
                return false;
            case CellDataType.Hex64:
                var h64 = t.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? t[2..] : t;
                if (ulong.TryParse(h64, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var hu64))
                {
                    words = Split64(hu64, order); return true;
                }
                return false;
            case CellDataType.Double:
                if (double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out var d))
                {
                    words = Split64(unchecked((ulong)BitConverter.DoubleToInt64Bits(d)), order); return true;
                }
                return false;

            default:
                return false;
        }
    }

    public static bool TryParseCoil(string text, out bool value)
    {
        value = false;
        var trimmed = text?.Trim().ToLowerInvariant() ?? "";
        if (trimmed is "1" or "true" or "on")  { value = true;  return true; }
        if (trimmed is "0" or "false" or "off") { value = false; return true; }
        return false;
    }

    // ─────────────────────────────────────────────────────────────────
    //  Word/byte combining (32-bit and 64-bit)
    //
    //  ABCD = BigEndian        (high word first, high byte first)
    //  CDAB = LittleEndian     (word swap)
    //  BADC = BigEndianByteSwap(byte swap within each word)
    //  DCBA = LittleEndianByteSwap (both)
    // ─────────────────────────────────────────────────────────────────

    private static uint Combine32(int[] words, WordOrder order)
    {
        ushort w0 = (ushort)words[0];
        ushort w1 = (ushort)words[1];
        return order switch
        {
            WordOrder.BigEndian            => ((uint)w0 << 16) | w1,
            WordOrder.LittleEndian         => ((uint)w1 << 16) | w0,
            WordOrder.BigEndianByteSwap    => ((uint)Swap16(w0) << 16) | Swap16(w1),
            WordOrder.LittleEndianByteSwap => ((uint)Swap16(w1) << 16) | Swap16(w0),
            _ => ((uint)w0 << 16) | w1,
        };
    }

    private static int[] Split32(uint value, WordOrder order)
    {
        ushort hi = (ushort)(value >> 16);
        ushort lo = (ushort)value;
        return order switch
        {
            WordOrder.BigEndian            => new int[] { hi, lo },
            WordOrder.LittleEndian         => new int[] { lo, hi },
            WordOrder.BigEndianByteSwap    => new int[] { Swap16(hi), Swap16(lo) },
            WordOrder.LittleEndianByteSwap => new int[] { Swap16(lo), Swap16(hi) },
            _ => new int[] { hi, lo },
        };
    }

    private static ulong Combine64(int[] words, WordOrder order)
    {
        ushort w0 = (ushort)words[0];
        ushort w1 = (ushort)words[1];
        ushort w2 = (ushort)words[2];
        ushort w3 = (ushort)words[3];
        return order switch
        {
            WordOrder.BigEndian            => ((ulong)w0 << 48) | ((ulong)w1 << 32) | ((ulong)w2 << 16) | w3,
            WordOrder.LittleEndian         => ((ulong)w3 << 48) | ((ulong)w2 << 32) | ((ulong)w1 << 16) | w0,
            WordOrder.BigEndianByteSwap    => ((ulong)Swap16(w0) << 48) | ((ulong)Swap16(w1) << 32) | ((ulong)Swap16(w2) << 16) | Swap16(w3),
            WordOrder.LittleEndianByteSwap => ((ulong)Swap16(w3) << 48) | ((ulong)Swap16(w2) << 32) | ((ulong)Swap16(w1) << 16) | Swap16(w0),
            _ => ((ulong)w0 << 48) | ((ulong)w1 << 32) | ((ulong)w2 << 16) | w3,
        };
    }

    private static int[] Split64(ulong value, WordOrder order)
    {
        ushort h3 = (ushort)(value >> 48);
        ushort h2 = (ushort)(value >> 32);
        ushort h1 = (ushort)(value >> 16);
        ushort h0 = (ushort)value;
        return order switch
        {
            WordOrder.BigEndian            => new int[] { h3, h2, h1, h0 },
            WordOrder.LittleEndian         => new int[] { h0, h1, h2, h3 },
            WordOrder.BigEndianByteSwap    => new int[] { Swap16(h3), Swap16(h2), Swap16(h1), Swap16(h0) },
            WordOrder.LittleEndianByteSwap => new int[] { Swap16(h0), Swap16(h1), Swap16(h2), Swap16(h3) },
            _ => new int[] { h3, h2, h1, h0 },
        };
    }

    private static ushort Swap16(ushort v) => (ushort)((v << 8) | (v >> 8));

    private static string FormatBinary16(ushort value)
    {
        var bin = Convert.ToString(value, 2).PadLeft(16, '0');
        return $"{bin[..4]} {bin[4..8]} {bin[8..12]} {bin[12..16]}";
    }
}
