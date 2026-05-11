using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;

namespace OpenPoll.Views;

public partial class BinaryEditorView : Window
{
    private const int BitCount = 16;
    private readonly CheckBox[] _bits = new CheckBox[BitCount];

    public BinaryEditorView() : this(0) { }

    public BinaryEditorView(int initialValue)
    {
        InitializeComponent();
        BuildBitGrid();
        SetValue(initialValue);
        UpdateLabels();
    }

    private void BuildBitGrid()
    {
        for (int displayIndex = 0; displayIndex < BitCount; displayIndex++)
        {
            int bit = (BitCount - 1) - displayIndex;
            var label = new TextBlock
            {
                Text = bit.ToString(),
                HorizontalAlignment = HorizontalAlignment.Center,
                FontSize = 11
            };
            var checkBox = new CheckBox
            {
                HorizontalAlignment = HorizontalAlignment.Center
            };
            checkBox.IsCheckedChanged += (_, _) => UpdateLabels();
            _bits[bit] = checkBox;

            var cell = new StackPanel
            {
                Orientation = Orientation.Vertical,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            cell.Children.Add(label);
            cell.Children.Add(checkBox);
            BitsGrid.Children.Add(cell);
        }
    }

    private void SetValue(int value)
    {
        for (int b = 0; b < BitCount; b++)
            _bits[b].IsChecked = (value & (1 << b)) != 0;
    }

    private int GetValue()
    {
        int v = 0;
        for (int b = 0; b < BitCount; b++)
            if (_bits[b].IsChecked == true)
                v |= 1 << b;
        return v;
    }

    private void UpdateLabels()
    {
        var v = GetValue();
        DecimalView.Text = v.ToString();
        HexView.Text = "0x" + v.ToString("X4");
    }

    private void OnSend(object? sender, RoutedEventArgs e) => Close(GetValue());
    private void OnCancel(object? sender, RoutedEventArgs e) => Close(null);
}
