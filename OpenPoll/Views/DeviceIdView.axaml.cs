using System;
using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Interactivity;
using OpenPoll.Models;
using OpenPoll.Services;

namespace OpenPoll.Views;

public partial class DeviceIdView : Window
{
    private readonly PollDefinition _connection;
    private readonly ObservableCollection<Row> _rows = new();

    public DeviceIdView() : this(new PollDefinition()) { }

    public DeviceIdView(PollDefinition connection)
    {
        _connection = connection.Clone();
        InitializeComponent();
        ResultsGrid.ItemsSource = _rows;
    }

    private void OnRead(object? sender, RoutedEventArgs e)
    {
        _rows.Clear();
        StatusText.Text = "Reading…";

        var code = ReadDeviceIdCode.Regular;
        if (CodeBox.SelectedItem is ComboBoxItem item && item.Tag is string tag && byte.TryParse(tag, out var c))
            code = (ReadDeviceIdCode)c;

        byte objectId = 0;
        if (byte.TryParse(ObjectIdBox.Text, out var parsed)) objectId = parsed;

        using var session = new ModbusSession();
        var connect = session.Connect(_connection);
        if (!connect.Success)
        {
            StatusText.Text = $"Connect failed: {connect.Error}";
            return;
        }

        var result = session.ReadDeviceIdentification(code, objectId);
        if (!result.Success)
        {
            StatusText.Text = result.Error ?? "Read failed";
            return;
        }

        foreach (var obj in result.Value!.Objects)
            _rows.Add(new Row(obj.Id, obj.Name, obj.Value));

        var more = result.Value.MoreFollows ? $" · more @ 0x{result.Value.NextObjectId:X2}" : "";
        StatusText.Text = $"{_rows.Count} object(s) · conformity 0x{result.Value.ConformityLevel:X2}{more}";
    }

    private void OnClose(object? sender, RoutedEventArgs e) => Close();

    public sealed record Row(byte Id, string Name, string Value)
    {
        public string IdHex => $"0x{Id:X2}";
    }
}
