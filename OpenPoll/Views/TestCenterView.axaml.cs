using System;
using System.Globalization;
using System.Linq;
using System.Text;
using Avalonia.Controls;
using Avalonia.Interactivity;
using OpenPoll.Models;
using OpenPoll.Services;

namespace OpenPoll.Views;

/// <summary>
/// Hand-craft a Modbus PDU as hex bytes, send it through the active session, and inspect the
/// raw response. Intended for protocol experimentation and verifying odd function codes that
/// don't have a dedicated UI surface.
/// </summary>
public partial class TestCenterView : Window
{
    private readonly PollDefinition _connection;
    private readonly StringBuilder _log = new();

    public TestCenterView() : this(new PollDefinition()) { }

    public TestCenterView(PollDefinition connection)
    {
        _connection = connection.Clone();
        InitializeComponent();
    }

    private void OnSend(object? sender, RoutedEventArgs e)
    {
        var raw = PduInput.Text ?? "";
        byte[] pdu;
        try { pdu = ParseHexBytes(raw); }
        catch (Exception ex) { Append($"Parse error: {ex.Message}"); return; }
        if (pdu.Length == 0) { Append("Empty PDU"); return; }

        using var session = new ModbusSession();
        var connect = session.Connect(_connection);
        if (!connect.Success) { Append($"Connect failed: {connect.Error}"); return; }

        var result = session.SendRawPdu(pdu);
        if (!result.Success)
        {
            Append($"TX  {Hex(pdu)}");
            Append($"ER  {result.Error}");
            return;
        }
        var response = result.Value!;
        Append($"TX  {Hex(pdu)}");
        Append($"RX  {Hex(response)}");
        if (response.Length >= 2 && (response[0] & 0x80) != 0)
            Append($"    → Modbus exception {response[1]:X2}");
        else if (response.Length >= 1)
            Append($"    → FC {response[0]:X2}, {response.Length} byte(s)");
    }

    private void OnClear(object? sender, RoutedEventArgs e)
    {
        _log.Clear();
        LogOutput.Text = "";
    }

    private void OnClose(object? sender, RoutedEventArgs e) => Close();

    private void Append(string line)
    {
        _log.AppendLine($"{DateTime.Now:HH:mm:ss.fff}  {line}");
        LogOutput.Text = _log.ToString();
    }

    private static string Hex(byte[] bytes) =>
        BitConverter.ToString(bytes).Replace("-", " ");

    /// <summary>Parse a free-form hex string into raw bytes. Tolerates separators (space, comma, dash, colon).</summary>
    public static byte[] ParseHexBytes(string raw)
    {
        var cleaned = new string(raw.Where(c => !char.IsWhiteSpace(c) && c != ',' && c != '-' && c != ':' && c != ';').ToArray());
        if (cleaned.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) cleaned = cleaned[2..];
        if (cleaned.Length % 2 != 0) throw new FormatException("Hex string must contain an even number of digits");
        var bytes = new byte[cleaned.Length / 2];
        for (int i = 0; i < bytes.Length; i++)
            bytes[i] = byte.Parse(cleaned.AsSpan(i * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        return bytes;
    }
}
